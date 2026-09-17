# Observabilidade do CoreFinance

> Laboratório de observabilidade aplicado à API do CoreFinance.
> Execução e validação: **2026-09-17**. Plano completo em [`.claude/plans/Observabilidade/`](../.claude/plans/Observabilidade/00-visao-geral.md).

---

## 1. Objetivo

Este laboratório existe para responder, **com dado e não com achismo**, dez perguntas sobre a API:

| # | Pergunta | Quem responde |
|---|---|---|
| 1 | A aplicação está saudável? | Health checks |
| 2 | Ela está disponível? | Health checks + alerta |
| 3 | Quantas requisições estão acontecendo? | Métricas |
| 4 | Qual a taxa de erro? | Métricas |
| 5 | Qual o tempo de resposta? | Métricas (P95/P99) |
| 6 | Onde está a lentidão? | Traces |
| 7 | Qual componente causou? | Traces |
| 8 | O que aconteceu naquela requisição? | Logs |
| 9 | Como achar todos os logs de uma requisição? | Correlação por `TraceId` |
| 10 | Como ser avisado quando der errado? | Alertas |

A regra que orienta tudo: **observabilidade é preocupação transversal e opcional**. Toda a stack
sobe atrás do profile `obs` do Docker Compose. Com ela desligada, a API continua respondendo
normalmente — se algum dia isso deixar de ser verdade, o desenho está errado.

Ordem de leitura de um incidente: **métrica** ("quanto / quão rápido") → **trace** ("onde, nesta
requisição") → **log** ("o que exatamente aconteceu").

---

## 2. Arquitetura

```text
                    CoreFinance.API (:8080)
                            |
        +-------------------+-------------------+
        |                                       |
     Serilog                            OpenTelemetry SDK
        |                                       |
        | HTTP push                             | OTLP gRPC :4317
        v                                       v
      Loki  <-------------------------  otel-collector
     :3100                                /            \
                                  prometheus         otlp/tempo
                                  exporter :8889          |
                                        ^                 v
                                   scrape 15s          Tempo :3200
                                        |                 |
                                  Prometheus :9090        |
                                        |                 |
        +-------------------------------+-----------------+
                                        |
                                  Grafana :3001
                                        |
                            Dashboard + Alertas provisionados
```

O SQL Server roda **fora** do compose (`sqlserver_container`, alcançado por
`host.docker.internal,1433`). Ele entra aqui só como dependência monitorada pelo health check de
readiness.

**Métricas e traces passam pelo Collector; logs vão direto ao Loki.** São dois modelos de
propósito: o Collector desacopla a aplicação do backend (trocar o Prometheus por outra coisa não
toca em código C#), e o Serilog direto mostra a linha de código virando log pesquisável, com
controle total dos labels.

---

## 3. Componentes

| Ferramenta | Versão | Papel | O que ela **não** faz |
|---|---|---|---|
| Health Check | ASP.NET Core 8 | estado da aplicação e das dependências | não conta requisição, não mede latência |
| Serilog | sink `Serilog.Sinks.Grafana.Loki` | **gerar** logs estruturados | não armazena, não pesquisa |
| Loki | `grafana/loki:3.4.2` | **armazenar e consultar** logs | indexa labels, não conteúdo |
| OpenTelemetry SDK | 1.18.0 (.NET) | instrumentar e transportar sinais | não armazena, não desenha gráfico |
| OTel Collector | `otel/opentelemetry-collector-contrib:0.121.0` | receber, processar, distribuir | não guarda histórico |
| Prometheus | `prom/prometheus:v3.2.1` | **armazenar e consultar** métricas | não guarda log, trace ou alta cardinalidade |
| Tempo | `grafana/tempo:2.7.1` | **armazenar e consultar** traces | não agrega métrica |
| Grafana | `grafana/grafana:11.6.1` | **visualizar** e **alertar** | não coleta nada sozinho |

Retenção: **7 dias** em todos os backends (Prometheus `--storage.tsdb.retention.time=7d`, Loki
`retention_period: 168h`, Tempo `block_retention: 168h`).

> ⚠️ Nunca use `:latest` aqui. Loki, Tempo e Collector quebram configuração entre versões maiores
> com frequência. Para atualizar, suba **uma imagem por vez** e valide antes da próxima.

---

## 4. Como subir

```powershell
# dia a dia — só api + web, exatamente como sempre foi
docker compose up -d

# laboratório completo — sobe também Loki, Grafana, Collector, Prometheus e Tempo
docker compose --profile obs up -d
```

O profile existe por dois motivos: a stack consome **1,2 a 1,8 GB de RAM**, e a observabilidade
não pode virar dependência do desenvolvimento. Quem clona o repositório e roda `docker compose up`
continua com api + web e nada mais.

> ⚠️ **Mexeu em C#? Use `--build api`.** Sem isso o compose reaproveita a imagem antiga e você
> valida a versão errada do código — erro que já custou uma validação inteira na fase 06.

---

## 5. URLs de acesso

| Serviço | URL | Observação |
|---|---|---|
| web | http://localhost:3000 | |
| api | http://localhost:5176 | Swagger em `/swagger` |
| **Grafana** | **http://localhost:3001** | ⚠️ **3001, não 3000** — a 3000 é do `web` |
| Prometheus | http://localhost:9090 | `/targets` mostra se a coleta está de pé |
| Loki | http://localhost:3100 | API; a consulta se faz pelo Grafana |
| Tempo | http://localhost:3200 | API de consulta |
| Collector | http://localhost:8889/metrics | o que o Prometheus enxerga, para `curl` |

Grafana: `admin` / `admin`. O acesso anônimo está ligado como `Viewer` (com
`GF_USERS_VIEWERS_CAN_EDIT=true`, senão o menu *Explore* desaparece para quem não loga).

Dashboard: **CoreFinance → ASP.NET Core Observability** (`/d/corefinance-obs`), em quatro faixas:
*1 — Estado agora*, *2 — Tráfego*, *3 — Latência*, *4 — Runtime e Logs*.

### Endpoints de demonstração

Só existem com `Observability:Demo:Enabled=true`. São `[AllowAnonymous]` e servem de matéria-prima
para todos os testes — provocam erro e lentidão **sem tocar em código de negócio**.

```text
GET  /api/demo/success
GET  /api/demo/error
GET  /api/demo/slow?delay=3000
GET  /api/demo/random?errorRate=5
POST /api/demo/alert-webhook     <- destino das notificações do Grafana
```

Gerador de carga (`scripts/gerar-carga.ps1`):

```powershell
.\scripts\gerar-carga.ps1                                   # misto, 60s
.\scripts\gerar-carga.ps1 -Cenario erro   -Duracao 180
.\scripts\gerar-carga.ps1 -Cenario lento  -Delay 3000 -Duracao 240
.\scripts\gerar-carga.ps1 -Cenario volume -Paralelo 8  -Duracao 60
```

> ⚠️ Os cenários válidos são **`misto`, `erro`, `lento` e `volume`**. Não existe `-Cenario sucesso`
> (o equivalente é `volume`, que bate em `/api/demo/success`).

---

## 6. Como testar logs

Grafana → **Explore** → datasource **Loki**.

```logql
{app="corefinance-api"}                                  # tudo
{app="corefinance-api", level="error"}                   # só erro
{app="corefinance-api"} | json | RequestPath="/api/demo/error"
{app="corefinance-api"} | json | CorrelationId="teste-006"
{app="corefinance-api"} | json | TraceId="<32 hex>"      # todos os logs de UMA requisição
sum by (level) (count_over_time({app="corefinance-api"}[5m]))
```

**Só existem três labels** — `app`, `env` e `level`. Todo o resto (`TraceId`, `CorrelationId`,
`RequestPath`, `StatusCode`, `Elapsed`…) é campo estruturado e exige `| json` antes do filtro.

> ⚠️ **`level` é minúsculo e normalizado.** O sink converte a convenção do Serilog para a do
> Grafana antes de enviar: `Information` → `info`, `Fatal` → `critical`. Filtrar por
> `level="Information"` **não retorna nada**.

> ⚠️ **`TraceId` nunca vira label.** Cada TraceId é único: como label, criaria um stream novo por
> requisição e derrubaria o Loki. Label é para o que tem poucos valores possíveis.

---

## 7. Como testar métricas

Grafana → **Explore** → **Prometheus**, ou direto em http://localhost:9090.

```promql
# taxa de requisições (fora do health)
sum(rate(http_server_request_duration_seconds_count{service_name="corefinance-api", http_route!~"/health.*"}[5m]))

# taxa de erro
sum(rate(http_server_request_duration_seconds_count{service_name="corefinance-api", http_route!~"/health.*", http_response_status_code=~"5.."}[5m]))
/
clamp_min(sum(rate(http_server_request_duration_seconds_count{service_name="corefinance-api", http_route!~"/health.*"}[5m])), 0.001)

# P95 global e por rota
histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{service_name="corefinance-api", http_route!~"/health.*"}[5m])))
histogram_quantile(0.95, sum by (le, http_route) (rate(http_server_request_duration_seconds_bucket{service_name="corefinance-api"}[5m])))

# média (para comparar com o P95)
sum(rate(http_server_request_duration_seconds_sum{service_name="corefinance-api"}[5m]))
/
sum(rate(http_server_request_duration_seconds_count{service_name="corefinance-api"}[5m]))

# saúde e runtime
corefinance_health_status
corefinance_health_check_status
corefinance_process_cpu_utilization_ratio
process_runtime_dotnet_gc_heap_size_bytes
process_runtime_dotnet_thread_pool_queue_length
process_runtime_dotnet_exceptions_count_total
```

> ⚠️ **`service.name` não vira `job`.** O `resource_to_telemetry_conversion` do Collector
> transforma resource attributes em labels, mas `job` e `instance` colidem com os que o próprio
> Prometheus injeta no scrape — os originais viram `exported_job` e `exported_instance`. Filtrar
> por `job="corefinance-api"` **não retorna nada**; o filtro certo é `service_name="corefinance-api"`.

> ⚠️ **`http_requests_total` não existe no .NET 8.** O nome real é
> `http_server_request_duration_seconds` (histograma: `_count`, `_sum` e `_bucket`). Antes de
> escrever qualquer query, leia `curl http://localhost:8889/metrics` e confirme os nomes.

---

## 8. Como testar traces

Grafana → **Explore** → **Tempo** → aba *TraceQL*.

```traceql
{ }                                          # tudo (últimos minutos)
{ duration > 2s }                            # requisições lentas
{ status = error }                           # requisições que quebraram
{ name = "GET api/Dashboard/anual" }         # uma rota específica
{ span.http.response.status_code = 500 }
```

Pela API, sem Grafana:

```powershell
curl "http://localhost:3200/api/search?q=%7B%20duration%20%3E%202s%20%7D&limit=3"
curl "http://localhost:3200/api/traces/<traceId>"
```

Trace real de negócio (`GET /api/dashboard/anual`), com o tempo por camada:

```text
GET api/Dashboard/anual                          128,6 ms   (ASP.NET Core)
  Dashboard.ObterAnual                            80,8 ms   (Application)
    Dashboard.ConsultarAno                        68,9 ms
      SELECT ... [Payments] ... [FixedAccounts]    8,1 ms   (SqlClient)
    Dashboard.ConsultarAno                         2,4 ms
      SELECT ... [Payments] ... [FixedAccounts]    2,0 ms
  Dashboard.Calcular                               8,4 ms
```

É isso que o trace entrega e a métrica não: **onde**, dentro da requisição, o tempo foi gasto.

> 💡 `/api/demo/slow` produz um trace de **um span só** — ele apenas dorme. Para ver a árvore
> completa (Application + SqlClient), use uma rota de negócio como a de cima.

---

## 9. Como testar health checks

```powershell
curl http://localhost:5176/health          # tudo, com detalhe por check
curl http://localhost:5176/health/live     # só "o processo responde?"
curl http://localhost:5176/health/ready    # dependências (SQL Server)
```

Resposta de `/health`:

```json
{"status":"Healthy","totalDuration":"00:00:00.0031090","entries":{
  "self":{"description":"API respondendo.","status":"Healthy","tags":["live"]},
  "sqlserver":{"status":"Healthy","tags":["ready","db"]}}}
```

Os três são **anônimos** e não exigem token. `live` responde "reinicie o container"; `ready`
responde "pare de mandar tráfego para cá" — são decisões diferentes de orquestrador, por isso são
endpoints diferentes.

Em série temporal: `corefinance_health_status` (1 saudável · 0.5 degradada · 0 fora) e
`corefinance_health_check_status{health_check="sqlserver"}`.

---

## 10. Como testar alertas

Três regras, provisionadas em `docker/grafana/provisioning/alerting/rules.yml`:

| Alerta | Condição | `for` | Severidade |
|---|---|---|---|
| High Error Rate | taxa de 5xx > 5 % | 1 min | critical |
| High Latency | P95 > 1 s | 2 min | warning |
| Application Unhealthy | `corefinance_health_status` < 1 | 1 min | critical |

Cada regra é sempre a mesma receita: **query (A) → reduce (B) → threshold (C)**.

Para disparar:

```powershell
.\scripts\gerar-carga.ps1 -Cenario erro  -Duracao 180              # High Error Rate
.\scripts\gerar-carga.ps1 -Cenario lento -Delay 3000 -Duracao 240  # High Latency
docker stop sqlserver_container                                    # Application Unhealthy
```

Acompanhar o estado sem abrir a UI:

```powershell
curl -u admin:admin http://localhost:3001/api/prometheus/grafana/api/v1/rules
```

O destino da notificação é a própria aplicação (`POST /api/demo/alert-webhook`), que loga o alerta
recebido. **O alerta vira log pesquisável no Loki** — a observabilidade passa a observar a si
mesma:

```logql
{app="corefinance-api"} |= "Alerta" | json
```

`firing` loga em `Warning`, `resolved` em `Information`, e o webhook devolve 200 sempre: devolver
erro faria o Grafana reenviar, e um webhook que falha sozinho viraria tráfego 5xx alimentando o
alerta de taxa de erro que acabou de disparar.

---

## 11. Como correlacionar Trace ID e logs

O `CorrelationIdMiddleware` aceita (ou gera) o header `X-Correlation-Id` e devolve o mesmo valor na
resposta. O `TraceId` do OpenTelemetry entra em toda linha de log via enricher.

**Log → trace.** No detalhe da linha de log no Explore existe o botão **Ver trace**. Ele nasce do
`derivedFields` do datasource Loki, que aplica o regex `"TraceId":"([a-f0-9]{32})"` em cada linha.

**Trace → log.** Em qualquer span, o botão **Logs for this span** abre o Loki já filtrado. Vem do
`tracesToLogsV2` do datasource Tempo, com uma janela de folga de ±5 min — o span chega ao Tempo
antes de o log chegar ao Loki, que envia em lote.

Na mão, dado um `TraceId`:

```logql
{app="corefinance-api"} | json | TraceId="b0f6f7dde0968a212113686d62b52b90"
```

```powershell
curl http://localhost:3200/api/traces/b0f6f7dde0968a212113686d62b52b90
```

> ⚠️ **Os dois `$$` do provisioning não são erro de digitação.** O Grafana interpola `$` como
> variável de ambiente nos arquivos de provisioning. Com um cifrão só, o link nasce quebrado e
> **nenhum erro é reportado** — o botão simplesmente não funciona.

---

## 12. Como desligar

```powershell
docker compose --profile obs down      # para tudo, MANTÉM os dados (volumes)
docker compose --profile obs down -v   # para tudo e APAGA os volumes
```

> ⚠️ A diferença entre `down` e `down -v` é a diferença entre "fechei o laboratório" e "joguei
> fora o histórico". No dia a dia, `down`. O `-v` só quando o objetivo é justamente provar que o
> ambiente se reconstrói do zero.

---

## 13. Linha de base medida (2026-09-17)

Sem número de referência não existe "anormal". Estes são os valores desta máquina, com a stack
inteira no ar; use-os como ponto de comparação, não como meta.

### Os 7 testes do plano

| # | Teste | Comando | Resultado observado |
|---|---|---|---|
| 1 | Health | `curl /health`, `/health/live`, `/health/ready` | `Healthy` nos três, HTTP 200, sem token. `/health` em **6,3 ms**, `self` em 0,0003 ms e `sqlserver` em 3,0 ms |
| 2 | Logs | `curl -H "X-Correlation-Id: fase09-teste02" /api/demo/error` | duas linhas `level="error"` no Loki em segundos, com stack trace, `TraceId=b0f6f7dd…`, `CorrelationId=fase09-teste02`, `StatusCode=500` |
| 3 | Métricas | `gerar-carga.ps1 -Cenario volume -Duracao 60 -Paralelo 8` | **120.972 req**, 100 % HTTP 200, **2.016 req/s** no cliente; target `otel-collector:8889` `up` no Prometheus |
| 4 | Latência | `misto` + `lento -Delay 3000` em paralelo, 240 s | **média 235 ms × P95 1,84 s × P99 4,22 s** — ver abaixo |
| 5 | Tracing | `{ duration > 2s }` no Tempo | 3 traces `GET api/Demo/slow` de 3.000–3.001 ms |
| 6 | Correlação | `TraceId` do log → `/api/traces/<id>` | trace encontrado, `service.name=corefinance-api`, `deployment.environment=local` |
| 7 | Alertas | `gerar-carga.ps1 -Cenario erro -Duracao 180` | `inactive` → `pending` (11:09:36) → `firing` (11:10:37), valor **0,92** (92 % de erro) |

### Teste 4 em detalhe — a lição mais transferível

Com tráfego **misto** (40,8 req/s, 4,9 % de erro, cauda curta) e **lento** (1,3 req/s a 3 s)
rodando ao mesmo tempo:

| Medida | Valor |
|---|---|
| Requisições/s | 31,0 |
| **Média** | **235 ms** |
| **P95** | **1,84 s** |
| P99 | 4,22 s |
| Taxa de erro | 4,7 % |

**1,3 requisição por segundo levando 3 s empurrou o P95 para quase 2 s enquanto a média ficou em
235 ms.** É exatamente o usuário que reclama e o gráfico que não mostra: a média dilui a cauda,
o percentil não.

> ⚠️ **Com o cenário `lento` puro isso não acontece.** Se *toda* requisição leva 3 s, média e P95
> coincidem (medido: média 3.006 ms, P95 3.010 ms no cliente). A divergência precisa de tráfego
> misto — é por isso que o teste 4 foi executado com os dois geradores simultâneos.

> ⚠️ **Rajada curta esconde o incidente no `rate([5m])`.** Logo após os 60 s do teste de volume,
> o P95 global marcava 4,7 ms e a média 0,2 ms, mesmo com 160 requisições de 3 s já registradas:
> 120.972 requisições rápidas na mesma janela diluíram tudo. Por rota, o número aparecia:
> `histogram_quantile(0.95, … by (le, http_route))` dava **4,875 s** em `api/Demo/slow` contra
> 4,7 ms em `api/Demo/success`. **Quando o agregado não mostra, quebre por dimensão.**

> 💡 **4,875 s para requisições de 3 s não é erro de medida — é o bucket.** `histogram_quantile`
> interpola dentro do bucket em que o percentil cai; com 3 s dentro da faixa `(2,5 s – 5 s]`, o
> valor devolvido é aproximado por interpolação linear. Histograma responde faixa, não valor
> exato: para latência precisa, o trace.

### Os 5 cenários de falha

| Cenário | Como provocar | O que foi observado |
|---|---|---|
| **HTTP 500** | `gerar-carga.ps1 -Cenario erro -Duracao 180` | 253.280 requisições, 100 % HTTP 500 a 1.407 req/s; taxa de erro 0,92 no Prometheus, logs `level="error"` com stack trace, alerta `firing` em 1 min |
| **Latência alta** | `-Cenario lento -Delay 3000` | P95 1,84 s com média em 235 ms; span de 3.000 ms no Tempo; alerta *High Latency* `firing` às 11:08 com valor **1,89 s** |
| **Dependência indisponível** | `docker stop sqlserver_container` | os três sinais ao mesmo tempo — ver abaixo |
| **Health unhealthy** | idem | `corefinance_health_status` = 0 e `corefinance_health_check_status{health_check="sqlserver"}` = 0 (com `self` = 1); `/health/ready` → **HTTP 503** |
| **Alto volume** | `-Cenario volume -Paralelo 8` | 2.016 req/s sustentados, 100 % de sucesso, P95 de 5 ms no cliente — saturação nenhuma nesta carga |

### O cenário que acende tudo: SQL Server fora do ar

É o exercício mais rico da trilha porque **um único evento aparece nos três sinais e nos três
alertas**. Linha do tempo real (`docker stop` às 11:17:48):

| Horário | O que aconteceu |
|---|---|
| 11:17:48 | `docker stop sqlserver_container` |
| 11:17:49 | `/health/ready` → **503 Unhealthy**, com a mensagem do TCP Provider no JSON. `/health/live` continua **Healthy** |
| 11:19:29 | `POST /api/auth/login` responde **500 em 14.587 ms** (timeout de conexão) |
| 11:19:35 | `corefinance_health_status` = 0 e alerta **Application Unhealthy** `firing` (~1 min 47 s depois da queda) |
| 11:21:08 | **High Error Rate** `firing` |
| 11:22:09 | **High Latency** `firing` — os três alertas disparados ao mesmo tempo |
| 11:22:17 | `docker start sqlserver_container` |
| 11:22:55 | métrica de saúde de volta a 1 (38 s depois) |
| 11:23:10 | **Application Unhealthy** de volta a `Normal` (53 s depois) |

O que cada caminho contou sobre o mesmo incidente:

- **Métrica** — `corefinance_health_check_status{health_check="sqlserver"}` = 0 com `self` = 1:
  a aplicação está viva, a dependência não. Dois alertas colaterais (erro e latência) nasceram
  do mesmo evento.
- **Log** — quatro linhas com o mesmo `TraceId`: o erro do EF Core (`An error occurred using the
  connection to database`), a exceção ao iterar o resultado, o `Erro não tratado` do middleware e
  a linha de request com `StatusCode=500` e `Elapsed=14587,5`.
- **Trace** — `POST api/Auth/login`, 14.591 ms, `STATUS_CODE_ERROR`, com a mensagem do SQL Server
  no status do span.

> ⚠️ **Dependência fora do ar não produz span de SQL.** A falha acontece ao **abrir a conexão**, e
> a instrumentação do `SqlClient` só cria span para comando executado. O trace tem um span só — o
> do servidor — e o "onde" vem da **mensagem de status**, não de um filho vermelho na árvore. Quem
> procura o span de banco para provar que o banco caiu não acha nada e conclui a coisa errada.

> 💡 **Dependência lenta ou morta vira incidente de latência, não só de erro.** Um login de ~100 ms
> virou 14,6 s por causa do timeout de conexão. É por isso que o alerta de latência disparou
> **sem nenhuma carga rodando**: bastaram algumas requisições travando no timeout.

### Reprodutibilidade — o ambiente nasce do zero

```powershell
docker compose --profile obs down -v        # apaga inclusive os volumes
docker compose --profile obs up -d --build
.\scripts\gerar-carga.ps1 -Duracao 120
```

Executado em 2026-09-17 com os quatro volumes removidos. Voltou **sem um único clique manual**:

| Item | Resultado |
|---|---|
| Datasources | `Loki` (default), `Prometheus`, `Tempo` — provisionados |
| Dashboard | `corefinance-obs` na pasta `CoreFinance` |
| Regras de alerta | as 3, mais o contact point `lab-local` e a notification policy |
| Target do Prometheus | `otel-collector:8889` `up` |
| Logs | 1.542 linhas em 2 min no Loki |
| Métricas | 19,9 req/s · P95 772 ms · taxa de erro 5,03 % |
| Traces | `{ status = error }` devolve traces de `GET api/Demo/random` |
| Correlação | `TraceId` do log abriu o trace no Tempo, com `STATUS_CODE_ERROR` |

> ⚠️ **O plano falava em "os 4 datasources". São 3** — Loki, Prometheus e Tempo. Não existe um
> quarto nesta implementação.

> ⚠️ **Nos primeiros segundos depois do `up`, pode existir log com `TraceId` cujo trace não
> existe.** O sink do Loki aceita o log na hora, mas o exportador OTLP ainda não tem Collector de
> pé e o span se perde no backoff. Buscar esse TraceId no Tempo devolve **404** — não é correlação
> quebrada, é ordem de partida. Espere o ambiente estabilizar antes de julgar.

### A aplicação continua funcionando sem observabilidade nenhuma

```powershell
docker compose --profile obs down
docker compose up -d
curl http://localhost:5176/health/live      # 200 em 2,3 ms
curl http://localhost:3000                  # 200 em 72 ms
curl http://localhost:5176/api/demo/success # 200 em 2 a 35 ms
```

Com toda a stack derrubada, a API subiu **saudável em 6 segundos** e respondeu no mesmo tempo de
sempre. O único efeito colateral é ruído no console:

```text
Exception while emitting periodic batch from Serilog.Sinks.Grafana.Loki.LokiSink:
System.Net.Http.HttpRequestException: Name or service not known (loki:3100)
```

É o sink tentando entregar e falhando **sem afetar a requisição** — exatamente o comportamento
desejado. O exportador OTLP, esse, falha em silêncio. Para calar os dois:
`Observability__Enabled: "false"` no compose.

---

## 14. Troubleshooting

### Não aparece **log** no Loki

1. `docker logs corefinance-loki --tail 50` — o Loki subiu?
2. `curl "http://localhost:3100/loki/api/v1/labels"` — responde?
3. A query usa `level="error"` (minúsculo) e não `"Error"`?
4. O filtro é por campo estruturado sem `| json` antes? Só `app`, `env` e `level` são labels.
5. Ative `Serilog.Debugging.SelfLog` — **o sink falha em silêncio por design**: se o Loki recusar
   o envio, a aplicação não reclama (e é assim que tem de ser).
6. No compose, `Serilog__WriteTo__1__Args__uri` aponta para o índice **1** do array `WriteTo`. Se
   alguém inserir um sink antes do `GrafanaLoki` no `appsettings.json`, esse índice passa a
   sobrescrever o sink errado e o log some sem erro.

### Não aparece **métrica** no Prometheus

1. http://localhost:9090/targets — o target `otel-collector:8889` está `UP`?
2. `curl http://localhost:8889/metrics | grep http_server` — o Collector está exportando?
3. `docker logs corefinance-otel-collector --tail 50` — recebeu OTLP?
4. A query filtra por `job="corefinance-api"`? **Troque para `service_name="corefinance-api"`.**
5. Espere: `OTEL_METRIC_EXPORT_INTERVAL` é 15 s e o scrape também. Até 30 s de atraso é normal.

### Não aparece **trace** no Tempo

1. `curl http://localhost:3200/ready`.
2. `docker logs corefinance-tempo --tail 50` — `permission denied` em `/var/tempo` significa que
   o `user: "0:0"` do compose foi removido.
3. O pipeline `traces` existe no `otel-collector-config.yml` com o exporter `otlp/tempo`?
4. Janela de tempo do Explore: o Tempo indexa por horário do span; procurar "últimos 5 minutos"
   um trace de 20 minutos atrás não devolve nada.

### O botão **Ver trace** não aparece na linha de log

1. O `matcherRegex` casa com o formato atual do log? Valide no Explore com
   `{app="corefinance-api"} | json | line_format "{{.TraceId}}"`.
2. Faltou o `$$` em `url` no `datasources.yml`.
3. Trocar o `textFormatter` do sink quebra o regex **em silêncio**.

### O alerta não dispara

1. `curl -u admin:admin http://localhost:3001/api/prometheus/grafana/api/v1/rules` — a regra existe
   e está `ok`?
2. A pasta `provisioning/alerting/` existia quando o Grafana subiu? Se não, ele loga
   `can't read alerting provisioning files from directory` e **segue em frente sem regra nenhuma**.
3. Some as esperas antes de achar que quebrou: publisher de health (15 s) + export OTLP (15 s) +
   scrape (15 s) + `for` (1 min) ≈ **2 minutos** até o `firing`. Latência de detecção é a soma da
   cadeia inteira, nunca só o `for`.

### A API está lenta / logando erro sem o profile `obs`

É esperado: o SDK tenta exportar OTLP para um Collector que não existe e loga falha em backoff.
Para silenciar, `Observability__Enabled: "false"` no compose. O que **não** pode acontecer é a
requisição falhar por causa disso.

---

## 15. Decisões e trade-offs

| Decisão | Escolha | Por quê |
|---|---|---|
| Como sobe | `profiles: [obs]` no mesmo `docker-compose.yml` | `docker compose up -d` continua subindo só api + web. Impacto zero no dia a dia; ~1,5 GB de RAM só quando se quer estudar |
| Métricas e traces | via **OTel Collector** | trocar backend não toca a aplicação; e a API usa só pacotes OTel **estáveis** (o exporter Prometheus in-process ainda é beta) |
| Logs | **Serilog direto para o Loki** | caminho mais didático: controle total dos labels e do formato. O contraste com o modelo via Collector é, ele próprio, a lição |
| Cenários de falha | `DemoController` atrás de flag | provoca erro e lentidão sem tocar em código de negócio; some com `Observability:Demo:Enabled=false` |
| Destino do alerta | webhook para a própria API | sem Slack nem e-mail no lab, e o alerta vira log pesquisável — a observabilidade observa a si mesma |
| Labels do Loki | só `app`, `env`, `level` | label é para baixa cardinalidade. `TraceId` como label criaria um stream por requisição |
| Amostragem de traces | 100 % | é um lab. Em produção, tail sampling no Collector (todo trace com erro ou lento + uma fração dos normais) |
| CPU medida na mão | `corefinance.process.cpu.utilization` em `AppMetrics` | `Instrumentation.Runtime` não publica utilização e o pacote `Instrumentation.Process` nunca saiu de pre-release |
| Health como métrica | `IHealthCheckPublisher` | transforma "está saudável agora?" em série temporal — dá para ver **quando** e **por quanto tempo** ficou ruim, e alertar sem o Grafana bater HTTP na API |
| `allowUiUpdates: true` | editar pela UI é permitido | o preço: a edição vive no banco do Grafana e **morre no `down -v`**. A fonte da verdade continua sendo o JSON no repositório |

### O que surpreendeu (e não está em tutorial nenhum)

- `http_requests_total` **não existe** no .NET 8 — é `http_server_request_duration_seconds`.
- `job="corefinance-api"` não filtra nada; o label certo é `service_name`, e os originais viram
  `exported_job` / `exported_instance`.
- O `level` do Loki é minúsculo e normalizado pelo sink (`Information` → `info`).
- O `$$` obrigatório no `url` do derived field.
- Loki schema **v13 + tsdb** — os exemplos antigos com `boltdb-shipper` não sobem na 3.x.
- Tempo roda como UID 10001 e não escreve em volume nomeado; sem `user: "0:0"` entra em restart
  loop com a causa escondida no meio do log.
- O contact point padrão (`email receiver`) **não some** com o provisioning; quem decide o destino
  é a notification policy, e a policy provisionada substitui a árvore inteira.
- `SetDbStatementForText` não existe mais na instrumentação de SQL estável (1.15+).

### Ruído conhecido

Ligar uma instrumentação faz o **próprio caminho de observabilidade virar sinal**. Três fontes
perpétuas já identificadas e filtradas: a rota `/health`, o push do Serilog para o Loki (via
`HttpClient`) e a sondagem `SELECT 1;` do readiness (via `SqlClient`, fora de qualquer requisição).

> 💡 **Teste de 30 segundos depois de ligar qualquer sinal novo:** deixe o ambiente parado, sem
> tocar em nada, e consulte tudo. O que aparecer é ruído — e ruído não some sozinho: custa storage
> e atenção para sempre.
