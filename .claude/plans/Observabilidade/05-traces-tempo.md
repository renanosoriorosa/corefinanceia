# Fase 05 — Distributed Tracing: OpenTelemetry + Tempo

> ⬅️ anterior: [04 — Métricas](04-metricas-otel-collector-prometheus.md) · ➡️ próxima: [06 — Correlação](06-correlacao-traceid-logs-traces.md)
> **Containers novos:** `tempo`.
> **Status:** ✅ concluída e validada em 2026-09-08. O que a execução mudou em relação ao plano
> está em [Resultado da execução](#resultado-da-execucao), no fim do documento.

---

## Objetivo pedagógico

Responder às perguntas **6 e 7**: *onde está a lentidão?* e *qual componente está causando o problema?*

A métrica disse "o P95 é 3 segundos". O trace mostra **em que exatamente** esses 3 segundos foram gastos: 2,8 s numa query do EF Core, 150 ms de serialização, 50 ms no resto. É a passagem de "tem problema" para "o problema é aqui".

---

## O que entra no projeto

**Pacotes:**

```xml
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.18.*" />
```

Os demais (`Extensions.Hosting`, `Instrumentation.AspNetCore`, `.Http`, `Exporter.OpenTelemetryProtocol`) já entraram na fase 04 e servem métrica **e** trace.

> ⚠️ **O plano pedia `1.11.0-beta.*` e dizia que o pacote ainda era beta. Não é mais.** As convenções semânticas de banco estabilizaram e a instrumentação saiu de beta na 1.15. A observação continua valendo como *lição*, só que invertida: a API mudou junto com a especificação e **`SetDbStatementForText` não existe mais** — ver [Resultado da execução](#resultado-da-execucao). Copiar configuração de tutorial escrito na época beta não compila.

**Arquivos novos:**

```text
docker/tempo/tempo.yml
src/CoreFinance.Application/Common/Observability/CoreFinanceActivitySource.cs
```

**Arquivos alterados:**

```text
src/CoreFinance.API/Observability/ObservabilityExtensions.cs   ← .WithTracing(...)
src/CoreFinance.API/Middlewares/GlobalExceptionMiddleware.cs   ← marcar a Activity como erro
src/CoreFinance.Application/Dashboard/Services/DashboardService.cs ← spans internos
docker/otel-collector/otel-collector-config.yml                ← pipeline traces
docker/grafana/provisioning/datasources/datasources.yml        ← datasource Tempo
docker-compose.yml
```

---

## Passos

### 1. `.WithTracing(...)`

```csharp
.WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation(options =>
    {
        // health check e métrica não precisam virar trace — é ruído puro
        options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
        options.RecordException = true;
    })
    // Sem este filtro, cada push do Serilog para o Loki vira um trace. Ver "os três ralos
    // de ruído" no resultado da execução.
    .AddHttpClientInstrumentation(options =>
        options.FilterHttpRequestMessage = req => !EhTelemetriaSaindo(req.RequestUri))
    .AddSqlClientInstrumentation(options =>
    {
        // db.query.text já vem por padrão — SetDbStatementForText foi removido do pacote
        options.RecordException = true;
        // a sondagem "SELECT 1;" do readiness roda fora de requisição e viraria trace órfão
        options.Filter = cmd =>
            cmd is not SqlCommand c || c.CommandText != HealthCheckExtensions.ConsultaDeSaude;
    })
    .AddSource(CoreFinanceActivitySource.Name)
    .AddOtlpExporter());
```

> ⚠️ **O texto do SQL vai para o span, e agora sem nada para ligar.** No plano isso era opt-in (`SetDbStatementForText`); na versão estável passou a ser o padrão. O risco não sumiu junto com a opção: em produção esse texto chega ao backend de traces, e o Tempo deste lab não tem controle de acesso. O que continua **fora** por padrão são os *valores* dos parâmetros, atrás de `OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS` — e é onde mora o dado de cliente. Deixe assim.

### 2. Pipeline de traces no Collector

```yaml
exporters:
  otlp/tempo:
    endpoint: tempo:4317
    tls:
      insecure: true        # rede interna do compose, sem TLS

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlp/tempo]
```

Nada muda na aplicação: ela já mandava OTLP para o Collector desde a fase 04. **Esse é o ponto inteiro de ter um Collector** — adicionar um backend de traces é um bloco de YAML, não um rebuild da API.

### 3. `docker/tempo/tempo.yml`

```yaml
server:
  http_listen_port: 3200

distributor:
  receivers:
    otlp:
      protocols:
        grpc:
          endpoint: 0.0.0.0:4317

ingester:
  max_block_duration: 5m

compactor:
  compaction:
    block_retention: 168h      # 7 dias

storage:
  trace:
    backend: local
    local:
      path: /var/tempo/blocks
    wal:
      path: /var/tempo/wal
```

### 4. Compose

```yaml
  tempo:
    image: grafana/tempo:2.7.1
    container_name: corefinance-tempo
    profiles: ["obs"]
    user: "0:0"                                # ver armadilha abaixo
    command: ["-config.file=/etc/tempo/tempo.yml"]
    ports:
      - "3200:3200"
    volumes:
      - ./docker/tempo/tempo.yml:/etc/tempo/tempo.yml:ro
      - tempo-data:/var/tempo
    restart: unless-stopped
```

E o datasource:

```yaml
  - name: Tempo
    type: tempo
    uid: tempo
    access: proxy
    url: http://tempo:3200
    jsonData:
      nodeGraph:
        enabled: true
```

> ⚠️ **O container do Tempo roda como UID 10001** e quebra ao escrever no volume com `permission denied` — o container entra em loop de restart e a mensagem fica escondida no meio do log. `user: "0:0"` resolve para um lab local. A alternativa correta (init container ajustando dono do volume) é overkill aqui, mas saiba que é o caminho certo em produção.

### 5. Span customizado no `DashboardService`

```csharp
// Application/Common/Observability/CoreFinanceActivitySource.cs
public static class CoreFinanceActivitySource
{
    public const string Name = "CoreFinance.Application";
    public static readonly ActivitySource Instance = new(Name, "1.0.0");
}
```

`ActivitySource` é da BCL (`System.Diagnostics`), não do pacote OpenTelemetry — então a `Application` não ganha dependência de vendor. Vale registrar: **a API de tracing do .NET é nativa; o OpenTelemetry só a coleta.**

`DashboardService.ObterAnualAsync` é o alvo perfeito: já faz **duas** consultas (ano corrente e ano anterior) e vários cálculos:

```csharp
using var activity = CoreFinanceActivitySource.Instance.StartActivity("Dashboard.ObterAnual");
activity?.SetTag("dashboard.ano", ano);
activity?.SetTag("dashboard.incluir_nao_fixas", incluirNaoFixas);

// spans filhos para os blocos caros
using (var calc = CoreFinanceActivitySource.Instance.StartActivity("Dashboard.CalcularComparativo")) { ... }
```

Depois disso o trace de `GET /api/dashboard/anual` mostra a árvore inteira:

```text
GET /api/dashboard/anual                     185 ms
├── Dashboard.ObterAnual                     180 ms
│   ├── SELECT Payments (ano)                 95 ms   ← SqlClient
│   ├── SELECT Payments (ano-1)               70 ms   ← SqlClient
│   └── Dashboard.CalcularComparativo          3 ms
└── (serialização)
```

Duas queries de ~85 ms cada em série, num endpoint de leitura — o trace acabou de sugerir uma otimização que nenhuma métrica mostraria.

### 6. Erro visível no trace

No `GlobalExceptionMiddleware`, no `catch`, antes de escrever a resposta:

```csharp
Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
Activity.Current?.AddException(ex);
```

Sem isso, uma requisição que retornou 500 aparece no Tempo como um trace comum, sem destaque. Com isso, nasce vermelho e é achável por `{status = error}`.

> ⚠️ **Aqui o `options.RecordException = true` da instrumentação do ASP.NET Core não salva você.** Este middleware **engole** a exceção para devolver o JSON de erro — logo a instrumentação nunca a vê. Quem tem `try/catch` global precisa marcar o span à mão, sempre.
>
> 💡 `AddException` é da BCL e chegou no .NET 9, mas funciona neste projeto `net8.0` porque o pacote `System.Diagnostics.DiagnosticSource` (9.x, trazido pelo OpenTelemetry) retropropaga a API. O `RecordException` do `OpenTelemetry.Trace` faz o mesmo e ainda existe — só que marcado `[Obsolete]`, e o build acusa.

---

## Como validar

```powershell
docker compose --profile obs up -d --build

curl "http://localhost:5176/api/demo/slow?delay=3000"
curl http://localhost:5176/api/demo/error
# e uma chamada real, com token, em /api/dashboard/anual?ano=2026
```

No Grafana → **Explore** → datasource **Tempo** → aba **Search** (ou TraceQL):

```traceql
{ }                                          # tudo (últimos minutos)
{ duration > 2s }                            # os lentos
{ status = error }                           # os que falharam
{ name = "GET api/dashboard/anual" }         # por endpoint
{ span.dashboard.ano = 2026 }                # pelo tag customizado
{ .service.name = "corefinance-api" && duration > 1s }
```

O roteiro completo (seção 13 da spec):

1. fazer a requisição → 2. achar o trace → 3. abrir → 4. ver a duração total → 5. ver os spans → 6. **guardar o Trace ID** — a [fase 06](06-correlacao-traceid-logs-traces.md) começa exatamente aí.

Confira também: o trace do `/api/dashboard/anual` mostra os spans de SQL do EF Core, e o do `/api/demo/error` aparece vermelho com a exceção anexada.

---

## Dicas e armadilhas

> 💡 **Vocabulário mínimo.** **Trace** = a requisição inteira. **Span** = uma unidade de trabalho dentro dela (tem nome, início, duração, tags, pai). **Trace ID** = 32 hex, igual para todos os spans do trace. **Span ID** = 16 hex, único por span. **Contexto de propagação** = header `traceparent` (padrão W3C), que atravessa serviços.

> 💡 **`Activity` **é** span.** O .NET criou `Activity` antes do OpenTelemetry existir; quando o padrão chegou, os conceitos foram unificados. Por isso o código usa `ActivitySource`/`Activity` e não uma classe `Span` — e por isso instrumentar não amarra você a nenhum fornecedor.

> ⚠️ **`using var activity = ...` — o `using` não é opcional.** Sem ele o span nunca termina, a duração fica errada e a hierarquia quebra. E `StartActivity` pode devolver `null` (quando ninguém está escutando aquela fonte): sempre use `activity?.`.

> ⚠️ **Filtre o health check do tracing.** Sem o `options.Filter`, o `/health/live` do Docker gera um trace a cada 30 s, para sempre. Em pouco tempo 90% do seu storage é ruído.

> 💡 **Amostragem (sampling) é o assunto que você vai encontrar em seguida.** Aqui está tudo em 100% porque o volume é baixo e o objetivo é ver tudo. Em produção usa-se *tail sampling* (guardar todo trace com erro ou lento, e uma fração dos normais) — e o lugar de configurar isso é o **Collector**, sem tocar na aplicação. Mais um ponto a favor da arquitetura escolhida.

> 💡 **Trace é o sinal mais caro.** Um trace de uma requisição com 20 spans é maior que a linha de log dela. Por isso: métrica sempre, trace amostrado, log com moderação.

> 💡 **Se o trace não aparecer:** (1) o Collector tem pipeline `traces`? (`docker logs corefinance-otel-collector`); (2) o Tempo subiu ou está em restart loop por permissão? (`docker ps`); (3) o intervalo de tempo do Grafana cobre agora?; (4) `/health` foi filtrado e você testou justamente nele?

---

## Conceitos aprendidos

- **Trace, span, hierarquia** e propagação de contexto (W3C `traceparent`).
- **Instrumentação automática vs manual** — o que vem de graça e o que só você sabe marcar.
- Por que `ActivitySource` na `Application` **não** viola Clean Architecture (é BCL, não vendor).
- **Sampling** e a economia dos sinais.
- Status de erro no span e por que ele precisa ser marcado explicitamente.
- O Collector como ponto onde se troca de backend sem tocar na aplicação.

---

## Critério de aceite

- [ ] Traces visíveis no Grafana via Tempo
- [ ] `{ duration > 2s }` encontra o `/api/demo/slow?delay=3000`
- [ ] `{ status = error }` encontra o `/api/demo/error`, com exceção anexada
- [ ] O trace de `/api/dashboard/anual` mostra os spans de SQL do EF Core
- [ ] Spans customizados de `Dashboard.*` aparecem aninhados
- [ ] `/health/*` **não** gera trace
- [ ] Tempo sobrevive a restart sem perder os blocos (volume ok)

---

<a id="resultado-da-execucao"></a>

## Resultado da execução (2026-09-08)

### Os três ralos de ruído

O plano previa **um**: o `/health` da rota HTTP. A execução encontrou **três**. Todos produzem trace
infinito, para sempre, sem ninguém pedir — e nenhum aparece até você olhar `{ }` num ambiente parado.

| Ralo | De onde vem | Como aparecia | Correção |
|---|---|---|---|
| `/health/*` pela rota | HEALTHCHECK do Docker, a cada 30 s | trace `GET health/live` | `options.Filter` na instrumentação do ASP.NET Core (previsto no plano) |
| Push do Serilog para o Loki | sink do Loki, a cada poucos segundos | trace raiz `POST` com `url.full = http://loki:3100/loki/api/v1/push` | `FilterHttpRequestMessage` na instrumentação de HttpClient |
| Sondagem `SELECT 1;` do readiness | `HealthCheckPublisher`, a cada 15 s | trace raiz **`SELECT`**, órfão | `options.Filter` na instrumentação de SqlClient |

Os dois novos têm a mesma raiz e ela merece nome: **telemetria observando a si mesma**. O sink de log
usa `HttpClient`, e `HttpClient` está instrumentado; o health check usa `SqlClient`, e `SqlClient`
está instrumentado. O do banco é o mais traiçoeiro porque **roda fora de qualquer requisição** — o
span nasce sem pai, então não é um span barulhento dentro de um trace: é um trace inteiro, a cada
15 s. Filtrar a rota `/health` não pega esse caso, porque ele não passa por rota nenhuma.

Antes da correção, uma janela de 2 minutos com uma única chamada real tinha ~15 traces. Depois:

```text
5 requisições reais    ->  5 traces
10 chamadas a /health  ->  0 traces
```

> 💡 A regra que sai daqui: **depois de ligar tracing, deixe o ambiente parado e consulte `{ }`.**
> Tudo que aparecer sem você ter feito nada é ruído, e ruído em trace custa storage e atenção para
> sempre. Este teste leva 30 segundos e o plano não o tinha.

### O trace do `/api/dashboard/anual`

Com o banco aquecido, o endpoint completo:

```text
GET api/Dashboard/anual                    15,3 ms
└── Dashboard.ObterAnual                   10,3 ms   {dashboard.ano=2026, dashboard.lancamentos=0}
    ├── Dashboard.ConsultarAno              5,9 ms   {papel=atual}
    │   └── SELECT [Payments] …             5,1 ms   ← SqlClient
    ├── Dashboard.ConsultarAno              4,2 ms   {papel=anterior}
    │   └── SELECT [Payments] …             3,6 ms   ← SqlClient
    └── Dashboard.Calcular                  0,1 ms
```

E aqui a fase entrega o que prometeu, com um resultado **diferente** do que o plano imaginava. O plano
apostava no `DashboardService` como "cálculo pesado". O trace desmente: `Dashboard.Calcular` é
**0,1 ms**, e as duas consultas sequenciais são **10,1 dos 10,3 ms** do serviço — praticamente o
método inteiro. Otimizar o cálculo não renderia nada; paralelizar as duas consultas, ou trazê-las
numa só, cortaria perto de 40 % da requisição.

Nenhuma métrica diria isso. `http_server_request_duration_seconds` mostra 15 ms e para por aí.

> 💡 **Isso é a fase inteira em um parágrafo.** A métrica te diz *que* está lento; o trace te diz
> *onde*; e, com alguma frequência, o *onde* contraria o palpite que você teria dado sem ele.

Na primeira chamada (conexão fria) os mesmos spans deram 132 ms, com a primeira consulta em 79,8 ms
contra 2,9 ms da segunda: o custo de abrir a conexão aparece embutido no primeiro `SELECT`. Vale
saber para não ler o primeiro trace pós-deploy como se fosse o comportamento normal.

Bônus não planejado: `POST api/Auth/login` levou **1.194 ms**, sem nenhum span de banco explicando o
tempo. É o BCrypt — trabalho de CPU, invisível para a instrumentação automática porque não é I/O.
Um span manual no `AuthService` mostraria isso; sem ele, o trace só sabe dizer "o tempo foi gasto
dentro do controller".

### Nomes e atributos reais

| O que | Valor observado |
|---|---|
| Nome do span de banco | `SELECT [Payments] [f].[Id] [f].[Active] …` (vem de `db.query.summary`, não é só `SELECT`) |
| Atributos do span de banco | `db.system.name`, `db.namespace`, `db.query.text`, `db.query.summary`, `server.address` |
| Texto da consulta | literais **redigidos**: `SELECT 1;` chega como `SELECT ?;` |
| Escopo dos spans manuais | `CoreFinance.Application` |
| Evento de exceção | `exception`, com `exception.type`, `exception.message`, `exception.stacktrace` |
| Status de erro | `STATUS_CODE_ERROR` + `message` |

> 💡 A redação de literais (`SELECT ?;`) é da convenção semântica nova, e suaviza — sem eliminar — o
> aviso de segurança do plano. O texto ainda revela estrutura de tabelas e colunas.

### TraceQL que funcionou

Todas verificadas contra a API do Tempo e pelo proxy do Grafana:

```traceql
{ }                                                  # tudo — use para caçar ruído
{ duration > 2s }                                    # achou o /api/demo/slow (3.011 ms)
{ status = error }                                   # achou o /api/demo/error, com a exceção
{ name = "GET api/Dashboard/anual" }
{ span.dashboard.ano = 2026 }                        # pelo tag customizado
{ resource.service.name = "corefinance-api" && duration > 1s }
```

> ⚠️ **O plano escrevia `{ .service.name = ... }`.** Funciona — o ponto sozinho procura em span *e*
> resource — mas é ambíguo e mais caro. `service.name` vem do **resource**, então o certo é
> `resource.service.name`; tags que você mesmo criou são `span.`. Diferente do Prometheus, onde
> `job` colide (fase 04), aqui o nome do serviço é consultável direto — mas com o prefixo certo.

> ⚠️ **Regex em TraceQL é sintaxe Go dentro de aspas.** `{ name =~ "Dashboard\\..*" }` devolve
> `invalid char escape`. Escreva `{ name =~ "Dashboard.*" }`.

### Desvios do plano original

| Desvio | Por quê |
|---|---|
| `Instrumentation.SqlClient` em `1.18.*`, estável, não `1.11.0-beta.*` | O pacote saiu de beta na 1.15, junto com a estabilização da convenção semântica de banco |
| `SetDbStatementForText` removido do código | A opção não existe mais na versão estável — `db.query.text` é o padrão. Dá `CS1061` no build |
| `AddException` em vez de `RecordException` | `RecordException` do `OpenTelemetry.Trace` está `[Obsolete]` na 1.18. `AddException` é BCL do .NET 9, disponível no `net8.0` via `System.Diagnostics.DiagnosticSource` 9.x |
| Filtro na instrumentação de HttpClient | Push do Serilog → Loki virava trace (ralo 2) |
| Filtro na instrumentação de SqlClient | Sondagem do readiness virava trace órfão (ralo 3) |
| `HealthCheckExtensions.ConsultaDeSaude` virou `const` pública | O filtro de tracing e o health check precisam da **mesma** string; duplicar o literal quebraria em silêncio no dia em que um dos dois mudasse |
| Span extra `Dashboard.ConsultarAno` (um por ano), além de `Dashboard.Calcular` | O plano previa só um span filho, para o cálculo. Sem separar as consultas, as duas idas ao banco ficariam indistinguíveis — e foi justamente essa separação que revelou o achado |
| `depends_on: [tempo]` no `otel-collector` | Só ordem de partida; evita ruído de conexão recusada no log inicial |
| Tempo publica só `3200`, não `4317` | Ninguém fora da rede do compose escreve no Tempo — quem escreve é o Collector |

> 💡 **Sobre o `user: "0:0"` do Tempo:** o plano avisava do restart loop por permissão e a precaução
> foi mantida desde o início. Com ela o container subiu de primeira, então o loop não chegou a ser
> observado — o aviso segue no plano como precaução aplicada, não como falha reproduzida.

### Pendências herdadas

- **Fase 06:** o Trace ID já está no log (enricher `WithSpan`, da fase 03) e o trace já está no Tempo. Falta o `derivedFields` do datasource Loki para o link ficar clicável nos dois sentidos.
- **Fase 07:** o painel de traces deve filtrar por `resource.service.name`, não por `.service.name`.
- **Ideia para a [fase 10](10-extras-e-proximos-passos.md):** span manual no `AuthService` para o BCrypt aparecer — é o exemplo mais didático de "trabalho de CPU que a instrumentação automática não vê".
