# Fase 03 — Logs estruturados: Serilog + Loki + Grafana

> ⬅️ anterior: [02 — Endpoints de demonstração](02-endpoints-de-demonstracao.md) · ➡️ próxima: [04 — Métricas](04-metricas-otel-collector-prometheus.md)
> **Containers novos:** `loki`, `grafana` — primeira fase que mexe no `docker-compose.yml`.
> **Status:** ✅ concluída e validada em 2026-09-05. Ver [O que a execução mudou](#o-que-a-execução-mudou-em-relação-ao-plano).

---

## Objetivo pedagógico

Responder à pergunta **8**: *o que aconteceu durante uma requisição específica?*

E entender a diferença entre **log de texto** e **log estruturado** — por que `"Pagamento 42 criado"` é quase inútil em escala e `"Pagamento {PaymentId} criado"` com `PaymentId=42` como campo é pesquisável, agrupável e alertável.

---

## O que entra no projeto

**Pacotes** (`CoreFinance.API.csproj`):

```xml
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />
<PackageReference Include="Serilog.Sinks.Grafana.Loki" Version="8.*" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.*" />
<PackageReference Include="Serilog.Enrichers.Span" Version="3.*" />
```

`Serilog.Enrichers.Span` entra desde já porque ele injeta `TraceId`/`SpanId` nos logs a partir da `Activity.Current` — é a semente da [fase 06](06-correlacao-traceid-logs-traces.md). Enquanto o OpenTelemetry não estiver ligado (fases 04/05), os campos vêm vazios; depois passam a ser preenchidos sozinhos.

**Arquivos novos:**

```text
docker/loki/loki-config.yml
docker/grafana/provisioning/datasources/datasources.yml
```

**Arquivos alterados:**

```text
src/CoreFinance.API/Program.cs        ← UseSerilog + UseSerilogRequestLogging
src/CoreFinance.API/appsettings.json  ← seção "Serilog" (substitui "Logging")
docker-compose.yml                     ← serviços loki e grafana no profile obs + env da api
```

---

## Passos

### 1. Configurar o Serilog no `Program.cs`

```csharp
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("app", "corefinance-api")
    .Enrich.WithProperty("env", context.HostingEnvironment.EnvironmentName));
```

`ReadFrom.Configuration` é o ponto importante: nível de log, sinks e URL do Loki vêm de `appsettings`/variável de ambiente, **não** hardcoded (seção 22 da spec).

E logo no início do pipeline:

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms";

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        // UserId sai do ICurrentUser — nunca o e-mail, nunca o token
    };
});
```

Isso cobre de uma vez os itens da seção 8 da spec: request recebida, request finalizada, status HTTP e tempo de execução — **em uma linha por requisição**, com campos separados.

> O `UseSerilogRequestLogging` **substitui** o ruído padrão do ASP.NET Core (3 linhas por requisição). Para isso, silencie `Microsoft.AspNetCore` em `MinimumLevel.Override`.

### 2. Seção `Serilog` no `appsettings.json`

```jsonc
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "WriteTo": [
    { "Name": "Console" },
    {
      "Name": "GrafanaLoki",
      "Args": {
        "uri": "http://loki:3100",
        "labels": [
          { "key": "app", "value": "corefinance-api" },
          { "key": "env", "value": "local" }
        ],
        "propertiesAsLabels": [ "level" ],
        "textFormatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
      }
    }
  ]
}
```

Três detalhes que decidem se este lab funciona ou não:

- **`propertiesAsLabels` restrito.** Por padrão o sink promove propriedades a label. Se `TraceId` ou `UserId` virarem label, cada requisição cria um *stream* novo no Loki e ele engasga em minutos. Só `level` (mais os fixos `app` e `env`) vira label.
- **`CompactJsonFormatter`** faz a linha de log ser JSON. Assim tudo que não é label continua pesquisável com `| json` no LogQL, sem custo de indexação.
- A **URI vem do ambiente** no compose (`Serilog__WriteTo__1__Args__uri`), ou use um placeholder `%LOKI_URL%`.

### 3. `docker/loki/loki-config.yml`

Configuração mínima para o Loki 3.x:

```yaml
auth_enabled: false

server:
  http_listen_port: 3100

common:
  instance_addr: 127.0.0.1
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1
  ring:
    kvstore:
      store: inmemory

schema_config:
  configs:
    - from: 2024-01-01
      store: tsdb
      object_store: filesystem
      schema: v13
      index:
        prefix: index_
        period: 24h

limits_config:
  retention_period: 168h        # 7 dias, suficiente para um lab
  allow_structured_metadata: true
```

> ⚠️ **Loki 3.x só aceita `schema: v13` + `store: tsdb`.** Praticamente todo tutorial de Loki que você encontrar usa `boltdb-shipper` + `v11` e **não sobe** no 3.x. Se o container morrer logo no start, leia `docker logs loki` — a mensagem sobre schema é explícita.

### 4. `docker/grafana/provisioning/datasources/datasources.yml`

```yaml
apiVersion: 1
datasources:
  - name: Loki
    type: loki
    uid: loki
    access: proxy
    url: http://loki:3100
    isDefault: true
```

Prometheus e Tempo entram neste mesmo arquivo nas fases 04 e 05, e os `derivedFields` do Loki na fase 06.

**Provisioning desde o primeiro dia** (seção 14 da spec): configurar datasource pela UI significa reconfigurar depois de todo `docker compose down`. Aqui a configuração é arquivo versionado.

### 5. `docker-compose.yml` — os dois primeiros serviços do profile

```yaml
  loki:
    image: grafana/loki:3.4.2
    container_name: corefinance-loki
    profiles: ["obs"]
    command: -config.file=/etc/loki/loki-config.yml
    ports:
      - "3100:3100"
    volumes:
      - ./docker/loki/loki-config.yml:/etc/loki/loki-config.yml:ro
      - loki-data:/loki
    restart: unless-stopped

  grafana:
    image: grafana/grafana:11.6.1
    container_name: corefinance-grafana
    profiles: ["obs"]
    ports:
      - "3001:3000"          # 3000 já é do web!
    environment:
      GF_SECURITY_ADMIN_USER: admin
      GF_SECURITY_ADMIN_PASSWORD: admin
      GF_AUTH_ANONYMOUS_ENABLED: "true"
      GF_AUTH_ANONYMOUS_ORG_ROLE: Viewer
      GF_USERS_DEFAULT_THEME: dark
    volumes:
      - ./docker/grafana/provisioning:/etc/grafana/provisioning:ro
      - ./docker/grafana/dashboards:/var/lib/grafana/dashboards:ro
      - grafana-data:/var/lib/grafana
    depends_on:
      - loki
    restart: unless-stopped

volumes:
  loki-data:
  grafana-data:
```

E no serviço `api`, a variável do sink:

```yaml
      Serilog__WriteTo__1__Args__uri: "http://loki:3100"
```

---

## Como validar

```powershell
docker compose --profile obs up -d --build

# Loki respondendo
curl http://localhost:3100/ready          # "ready"

# gerar log
curl http://localhost:5176/api/demo/success
curl http://localhost:5176/api/demo/error
```

No Grafana (`http://localhost:3001` → **Explore** → datasource **Loki**):

```logql
{app="corefinance-api"}                                  # tudo
{app="corefinance-api", level="Error"}                   # só erros (label)
{app="corefinance-api"} | json | StatusCode = 500        # campo do JSON
{app="corefinance-api"} | json | Elapsed > 1000          # requisições lentas
{app="corefinance-api"} |= "demo"                        # busca textual
```

Checagens finais:

- a linha de `UseSerilogRequestLogging` traz `StatusCode` e `Elapsed` **como campos**, não embutidos no texto;
- `docker compose --profile obs down && docker compose --profile obs up -d` → o datasource Loki **continua** configurado (prova do provisioning);
- `docker compose down && docker compose up -d` (sem profile) → api e web sobem normalmente; a API loga aviso de falha ao enviar para o Loki e **continua respondendo**.

---

## Dicas e armadilhas

> ⚠️ **A armadilha mais cara desta fase é a cardinalidade de label.** No Loki, cada combinação distinta de labels é um *stream* com índice próprio. `TraceId` como label = um stream por requisição = consumo de memória explodindo e consultas lentas. Regra prática: **label é o que você usa para escolher o que ler; campo é o que você usa para filtrar depois de ler.**

> ⚠️ **Não logue segredo.** Senha, token JWT, connection string e hash. Os pontos de atenção são `AuthController` e `AuthService` — nunca logue o `LoginRequest` inteiro. Se precisar identificar o usuário, use `UserId` (Guid), não o e-mail.

> 💡 **Níveis têm significado, use-os.**
> `Information` = fato de negócio ("pagamento criado"). `Warning` = anômalo mas tratado (login inválido). `Error` = falhou e alguém precisa olhar. `Debug`/`Trace` = desligados por padrão, ligados sob demanda.
> Se tudo é `Information`, o nível deixa de filtrar qualquer coisa.

> 💡 **Log estruturado é um contrato.** Manter o template estável (`"Pagamento {PaymentId} criado"`) permite ao Loki agrupar todas as ocorrências mesmo com IDs diferentes. Mudar o texto do template quebra painéis e alertas construídos sobre ele — trate template como API.

> 💡 **Console + Loki juntos, de propósito.** O console continua sendo o caminho mais rápido (`docker logs -f corefinance-api`) quando o Loki é justamente o que está quebrado. Redundância barata e intencional.

> 💡 **Se nada aparecer no Grafana**, siga a ordem: (1) `docker logs corefinance-api` — o Serilog está gerando? (2) `docker logs corefinance-loki` — recebeu, ou erro de schema? (3) o intervalo de tempo do Grafana está em "Last 5 minutes"? (4) `Serilog.Debugging.SelfLog.Enable(Console.Error)` no `Program.cs` revela erros do sink que normalmente são engolidos — o sink falha em silêncio **por design**, para não derrubar a aplicação.

---

## Conceitos aprendidos

- Log **estruturado** vs log de texto; template como chave de agrupamento.
- **Enrichers**: contexto adicionado uma vez, presente em todo log.
- Modelo de armazenamento do Loki: **índice de labels + conteúdo comprimido**, e por que isso o torna barato e por que cardinalidade o mata.
- **LogQL**: seletor de stream `{}` → parser `| json` → filtro de campo.
- **Provisioning** do Grafana: configuração como código.
- Observabilidade **não pode derrubar a aplicação**: o sink falha em silêncio de propósito.

---

## Critério de aceite

- [x] Logs estruturados no console, uma linha por requisição, com `StatusCode` e `Elapsed`
- [x] `{app="corefinance-api"}` retorna logs no Grafana
- [x] `| json | StatusCode = 500` isola os erros do `/api/demo/error`
- [x] Labels no Loki limitados a `app`, `env`, `level` (confira em Explore → *Label browser*)
- [x] `TraceId` presente como campo do JSON — **já preenchido**, não vazio (ver abaixo)
- [x] Datasource sobrevive a `down`/`up`
- [x] Sem o profile `obs`, a API continua respondendo normalmente

---

## O que a execução mudou em relação ao plano

Quatro coisas só apareceram quando a stack subiu de verdade. Todas estão aplicadas no código.

### 1. `TraceId` já vem preenchido — a fase 05 não é pré-requisito

O plano previa `TraceId` vazio até o OpenTelemetry entrar. Não é o que acontece: o próprio
ASP.NET Core abre uma `Activity` por requisição, e o `Enrich.WithSpan()` a encontra. Já saem
`TraceId`, `SpanId` e `ParentId` em toda linha, inclusive nos logs de dentro do controller.

O que a fase 05 acrescenta não é o TraceId — é o **destino** dele: um trace navegável no Tempo
para o mesmo id que o log já carrega.

### 2. `app` e `env` não podem ser enricher *e* label ao mesmo tempo

A primeira execução produziu a linha contraditória: label `env=local`, campo `env=Development`.
O `Enrich.WithProperty` usava `EnvironmentName` (conceito do ASP.NET), o label do sink usava
`local` (convenção do lab) — e como `propertiesAsLabels` só promove `level`, os dois sobreviviam
lado a lado.

Os dois enrichers saíram do `Program.cs`. Quem define `app` e `env` é o sink, em um lugar só.

### 3. O Loki 3.x inventa labels por conta própria

Sem configuração extra, apareceram **`service_name`** (derivado de `app`) e **`detected_level`**
(derivado do conteúdo da linha) — cópias de labels que a aplicação já manda de propósito. O
`limits_config` agora desliga os dois:

```yaml
  discover_service_name: []
  discover_log_levels: false
```

A lição vale além do lab: **quem decide o label é quem gera o log**, não o backend.

### 4. `/health` precisou de um `GetLevel` próprio

O `HEALTHCHECK` do Docker bate em `/health/live` a cada 30 segundos. Em `Information` isso vira
2.880 linhas por dia — de longe a maior fonte de log do laboratório, enterrando o sinal real.
`options.GetLevel` rebaixa health check bem-sucedido para `Debug` (abaixo do mínimo, então nem
sai) e o mantém em `Warning`/`Error` quando falha, que é justamente quando você quer vê-lo.

### 5. Viewer anônimo não enxergava o Explore

Sintoma: stack inteira no ar, Loki com dados, datasource conectado — e nada no Grafana.

Causa: no Grafana OSS 11, a role **Viewer não tem a permissão `datasources:explore`**, e sem ela
o item *Explore* simplesmente **some da navegação**. Como o `GF_AUTH_ANONYMOUS_ORG_ROLE` é
`Viewer`, quem abre `localhost:3001` sem logar não tem onde rodar uma query — que é a única
coisa que o lab faz nesta fase.

Corrigido no compose:

```yaml
      GF_USERS_VIEWERS_CAN_EDIT: "true"
```

Alternativa seria logar como `admin`/`admin` a cada visita, ou promover o anônimo a `Editor`.
`viewers_can_edit` é o menor dos três: mantém a role Viewer (não salva dashboard) e devolve só
o Explore.

> 💡 Diagnóstico que isola isso em um comando — compara o que cada usuário pode fazer:
> ```bash
> curl -s localhost:3001/api/access-control/user/permissions            # anonimo
> curl -s -u admin:admin localhost:3001/api/access-control/user/permissions
> ```

> 💡 E para provar que o problema é a UI, e não o pipeline, consulte o Loki **pelo proxy do
> Grafana** (o mesmo caminho que a tela usa):
> ```bash
> curl -s -u admin:admin -G >   'http://localhost:3001/api/datasources/proxy/uid/loki/loki/api/v1/query_range' >   --data-urlencode 'query={app="corefinance-api"}' --data-urlencode 'since=1h'
> ```
> Se isso retorna linha e a tela não mostra, o problema é permissão ou intervalo de tempo.

### Outros ajustes de execução

| O que | Por quê |
|---|---|
| `UseSerilogRequestLogging` **depois** do `GlobalExceptionMiddleware` | Se viesse antes, a exceção já teria virado resposta 500 e a requisição seria registrada em `Information`, sem stack trace. Depois, a exceção ainda está viva ao passar pelo middleware do Serilog: sai `Error` com stack. |
| `SelfLog` ligado em `Development` | O sink falha em silêncio por design. Sem isso, "não aparece nada no Grafana" não tem diagnóstico nenhum. |
| `uri` do sink = `http://localhost:3100` no `appsettings.json` | O valor de container (`http://loki:3100`) vive só no compose, via `Serilog__WriteTo__1__Args__uri`. Assim `dotnet run` na máquina também alcança o Loki. |
| `Microsoft` e `System` em `MinimumLevel.Override` | Sem eles ainda sobrava ruído de framework fora do namespace `Microsoft.AspNetCore`. |
| `/ready` do Loki responde 503 nos primeiros ~20s | `Ingester not ready: waiting for 15s after being ready` é a janela normal de warm-up, não erro. Depois vira `ready`. O `/` do Loki responde **404 para sempre**: ele não tem UI, é só API — a interface é o Grafana. |

### Ponta solta conhecida

`Failed to determine the https port for redirect.` aparece uma vez a cada start, vinda do
`UseHttpsRedirection` em um container que só expõe HTTP. É anterior a esta fase e inofensiva —
mas é exatamente o tipo de `Warning` recorrente que polui alerta. Candidata à fase 08.
