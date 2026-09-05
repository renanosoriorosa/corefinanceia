# Fase 05 — Distributed Tracing: OpenTelemetry + Tempo

> ⬅️ anterior: [04 — Métricas](04-metricas-otel-collector-prometheus.md) · ➡️ próxima: [06 — Correlação](06-correlacao-traceid-logs-traces.md)
> **Containers novos:** `tempo`.

---

## Objetivo pedagógico

Responder às perguntas **6 e 7**: *onde está a lentidão?* e *qual componente está causando o problema?*

A métrica disse "o P95 é 3 segundos". O trace mostra **em que exatamente** esses 3 segundos foram gastos: 2,8 s numa query do EF Core, 150 ms de serialização, 50 ms no resto. É a passagem de "tem problema" para "o problema é aqui".

---

## O que entra no projeto

**Pacotes:**

```xml
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.11.0-beta.*" />
```

Os demais (`Extensions.Hosting`, `Instrumentation.AspNetCore`, `.Http`, `Exporter.OpenTelemetryProtocol`) já entraram na fase 04 e servem métrica **e** trace.

> ⚠️ `Instrumentation.SqlClient` ainda é **beta** — as convenções semânticas de banco no OpenTelemetry mudaram algumas vezes e o pacote acompanha. Para um lab está ótimo; em produção, fixe a versão exata e leia o changelog antes de subir. É bom saber diferenciar "beta porque é instável" de "beta porque a especificação ainda se move" — aqui é o segundo caso.

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
    .AddHttpClientInstrumentation()
    .AddSqlClientInstrumentation(options =>
    {
        options.SetDbStatementForText = true;   // ver o SQL no span — ver aviso abaixo
        options.RecordException = true;
    })
    .AddSource(CoreFinanceActivitySource.Name)
    .AddOtlpExporter());
```

> ⚠️ **`SetDbStatementForText = true` grava o texto do SQL no span.** Num lab é o que torna o trace interessante. Em produção, é um risco: parâmetros e dados de cliente podem ir junto. Saiba o que está ligando.

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
