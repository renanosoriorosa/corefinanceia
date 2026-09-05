# Trilha de Observabilidade do CoreFinance — Visão geral

> **Status:** fases [01](01-health-checks.md) e [02](02-endpoints-de-demonstracao.md) concluídas e validadas em 2026-09-05 · fases 03-10 pendentes.
> **Referência:** `observalibidade-dicas.md` (spec original, escrita para um projeto novo).
> **Adaptação:** aplicar a spec ao CoreFinance que já existe, sem quebrar nada do que está no ar hoje.

---

## 1. Por que esta trilha existe

O objetivo é **didático**. Não é entregar uma feature de negócio — é aprender, na prática e no próprio código, como se constrói cada camada de observabilidade e como elas se conectam.

A spec original propunha um `observability-lab` do zero. Aqui ela é aplicada ao CoreFinance porque:

- o CoreFinance **já é** uma ASP.NET Core 8 dockerizada, com Clean Architecture, JWT, EF Core e SQL Server — a própria regra 26.2 da spec manda reaproveitar em vez de recriar;
- instrumentar um sistema real ensina mais do que instrumentar um "hello world": tem query de banco lenta, tem 401, tem multiusuário, tem cálculo pesado no `DashboardService`;
- a Fase 4 do `roadmap.md` já previa "Serilog estruturado + correlation id" e "health checks" — esta trilha **absorve** esses dois itens.

### As 10 perguntas que o laboratório precisa responder

| # | Pergunta | Respondida na fase |
|---|---|---|
| 1 | A aplicação está saudável? | [01 — Health Checks](01-health-checks.md) |
| 2 | A aplicação está disponível? | [01](01-health-checks.md) + [08 — Alertas](08-alertas.md) |
| 3 | Quantas requisições estão acontecendo? | [04 — Métricas](04-metricas-otel-collector-prometheus.md) |
| 4 | Qual a taxa de erro? | [04](04-metricas-otel-collector-prometheus.md) |
| 5 | Qual o tempo de resposta? | [04](04-metricas-otel-collector-prometheus.md) |
| 6 | Onde está ocorrendo a lentidão? | [05 — Traces](05-traces-tempo.md) |
| 7 | Qual componente está causando o problema? | [05](05-traces-tempo.md) |
| 8 | O que aconteceu durante uma requisição específica? | [03 — Logs](03-logs-serilog-loki.md) |
| 9 | Como achar todos os logs de uma requisição? | [06 — Correlação](06-correlacao-traceid-logs-traces.md) |
| 10 | Como alertar quando algo dá errado? | [08 — Alertas](08-alertas.md) |

---

## 2. Decisões de arquitetura (fechadas)

| Decisão | Escolha | Por quê |
|---|---|---|
| Como sobe no compose | `profiles: [obs]` no **mesmo** `docker-compose.yml` | `docker compose up -d` continua subindo só `api` + `web`, como hoje. `docker compose --profile obs up -d` sobe a stack inteira. Impacto zero no dia a dia e ~1,5 GB de RAM só quando se quer estudar. |
| Métricas e traces | **OTel Collector no meio**: API → OTLP → Collector → Prometheus / Tempo | Mais próximo de produção: trocar backend não toca a aplicação. Bônus prático: a API usa só pacotes OTel **estáveis** (o exporter Prometheus in-process ainda é beta). |
| Logs | **Serilog → Loki direto** (sink `Serilog.Sinks.Grafana.Loki`) | Caminho mais didático: dá para ver a linha exata de código virar log pesquisável, com controle total dos labels. O contraste entre os dois modelos (direto vs via Collector) é ele próprio uma lição — ver [10 — Extras](10-extras-e-proximos-passos.md). |
| Cenários de falha | `DemoController` atrás de flag | `/api/demo/{success,error,slow,random}`, `[AllowAnonymous]`, só existe com `Observability:Demo:Enabled=true`. Provoca erro e lentidão **sem tocar em código de negócio**. |

---

## 3. Arquitetura final

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

`sqlserver_container` continua **fora** do compose (a API o alcança por `host.docker.internal,1433`). Nada muda nisso — ele só entra como *dependência monitorada* no health check de readiness.

---

## 4. Convenções (valem para todos os documentos)

### Portas no host

`3000` já é do `web`, então **o Grafana vai para 3001**. Essa é a colisão mais fácil de esquecer.

| Serviço | Host | Container | Observação |
|---|---|---|---|
| web (existente) | 3000 | 3000 | — |
| api (existente) | 5176 | 8080 | — |
| **grafana** | **3001** | 3000 | ponto de entrada do lab |
| prometheus | 9090 | 9090 | |
| loki | 3100 | 3100 | |
| tempo | 3200 | 3200 | HTTP/consulta |
| otel-collector | 8889 | 4317 / 4318 / 8889 | publica só 8889, para `curl` de depuração |

### Identificação dos sinais

```text
service.name           = corefinance-api
service.namespace      = corefinance
deployment.environment = local
```

### Labels do Loki — **só estes três**

```text
app   = corefinance-api
env   = local
level = Information | Warning | Error | ...
```

**`TraceId` é campo estruturado, jamais label.** É a regra 12 da spec e o erro clássico de quem começa: cada TraceId é único, então virar label cria um stream novo por requisição e derruba o Loki. Label é para o que tem poucos valores possíveis.

### Volumes nomeados

`grafana-data`, `prometheus-data`, `loki-data`, `tempo-data`.

### Imagens fixadas

```text
grafana/grafana:11.6.1
prom/prometheus:v3.2.1
grafana/loki:3.4.2
grafana/tempo:2.7.1
otel/opentelemetry-collector-contrib:0.121.0
```

> 💡 **Dica:** nunca use `:latest` aqui. Loki, Tempo e Collector quebram config entre versões maiores com uma frequência alta. Quando quiser atualizar, **suba uma imagem de cada vez** e valide antes da próxima — assim você sabe qual mudança quebrou.

### Estrutura de arquivos que a execução vai criar

```text
docker/
  otel-collector/otel-collector-config.yml
  prometheus/prometheus.yml
  loki/loki-config.yml
  tempo/tempo.yml
  grafana/
    provisioning/datasources/datasources.yml
    provisioning/dashboards/dashboards.yml
    provisioning/alerting/contact-points.yml
    provisioning/alerting/rules.yml
    dashboards/corefinance-observability.json
scripts/
  gerar-carga.ps1
src/CoreFinance.API/
  Controllers/DemoController.cs
  Extensions/HealthCheckExtensions.cs
  Health/
  Observability/
  Middlewares/CorrelationIdMiddleware.cs
docs/OBSERVABILIDADE.md
```

---

## 5. Ordem das fases

Cada fase é **executável e validável sozinha**. Não começar a próxima antes da anterior estar validada de verdade (regra 26.14 da spec: *compilar não é aceite; comportamento observável é*).

| # | Documento | Containers novos | Ganho |
|---|---|---|---|
| ✅ 01 | [Health Checks](01-health-checks.md) | nenhum | "está viva?" — vitória rápida, só .NET |
| ✅ 02 | [Endpoints de demonstração](02-endpoints-de-demonstracao.md) | nenhum | matéria-prima para todas as fases seguintes |
| 03 | [Logs: Serilog + Loki](03-logs-serilog-loki.md) | `loki`, `grafana` | "o que aconteceu?" |
| 04 | [Métricas: OTel + Collector + Prometheus](04-metricas-otel-collector-prometheus.md) | `otel-collector`, `prometheus` | "quanto, quão rápido, quantos erros?" |
| 05 | [Traces: Tempo](05-traces-tempo.md) | `tempo` | "onde exatamente está lento?" |
| 06 | [Correlação](06-correlacao-traceid-logs-traces.md) | nenhum | amarra log ↔ trace — o coração do lab |
| 07 | [Dashboard](07-dashboard-grafana.md) | nenhum | uma tela responde "estou saudável agora?" |
| 08 | [Alertas](08-alertas.md) | nenhum | o sistema avisa você, você não fica olhando |
| 09 | [Testes e documentação](09-testes-e-documentacao.md) | nenhum | reprodutível do zero |
| 10 | [Extras](10-extras-e-proximos-passos.md) | opcionais | para depois, se quiser ir mais fundo |

> 💡 **Dica:** a ordem não é arbitrária. Ela vai do sinal mais barato (health = um endpoint) ao mais caro (traces = SDK + backend + storage), e cada fase produz algo que a próxima consome. Logs antes de métricas porque log é o sinal que você já sabe ler; métricas antes de traces porque a métrica te diz *que* tem problema e o trace te diz *onde*.

---

## 6. O que cada ferramenta resolve

| Ferramenta | Papel | O que ela **não** faz |
|---|---|---|
| **Health Check** | estado de saúde da aplicação e das dependências | não conta requisição, não mede latência |
| **Serilog** | **gerar** logs estruturados | não armazena, não pesquisa |
| **Loki** | **armazenar e consultar** logs | não gera log; não é banco relacional — indexa labels, não conteúdo |
| **OpenTelemetry** | **instrumentar e transportar** os sinais (padrão aberto) | não armazena nada, não desenha gráfico |
| **OTel Collector** | receber, processar e distribuir sinais | não é banco; não guarda histórico |
| **Prometheus** | **armazenar e consultar** métricas (série temporal) | não guarda log nem trace; não guarda alta cardinalidade |
| **Tempo** | **armazenar e consultar** traces | não agrega métrica (a menos que você ligue o metrics-generator) |
| **Grafana** | **visualizar** tudo e disparar **alertas** | não coleta nada sozinho |

> 💡 **Dica de mentalidade:** os três sinais respondem perguntas diferentes.
> **Métrica** = "quantos / quão rápido" (agregado, barato, sem detalhe).
> **Trace** = "onde, nesta requisição" (detalhado, caro, amostrado).
> **Log** = "o que exatamente aconteceu" (texto, caríssimo em volume).
> Você entra pela métrica, refina pelo trace e confirma pelo log. Essa é a espinha dorsal do lab.

---

## 7. Armadilhas globais (leia antes de começar)

**1. Com o profile `obs` desligado, a API reclama.**
Como o profile é opt-in, `docker compose up -d` sobe a API com `OTEL_EXPORTER_OTLP_ENDPOINT` apontando para um collector que não existe. O SDK vai tentar exportar, falhar e logar aviso a cada retry. **Isso é esperado** — e é o próprio conceito de *backoff de exportador*: a aplicação não pode cair porque a observabilidade caiu. Se incomodar, `Observability__Enabled=false` no compose.

**2. Consumo de recursos.**
Grafana + Prometheus + Loki + Tempo + Collector ≈ **1,2 a 1,8 GB de RAM**. No Docker Desktop do Windows, se a WSL estiver limitada, ajuste o `.wslconfig`. É o motivo principal de o profile ser opt-in.

**3. Nunca logar segredo.**
Senha, token JWT, connection string, hash. `AuthController` e `AuthService` são os pontos de atenção. Log vaza para o Loki, o Loki não tem controle de acesso neste lab.

**4. Não confie nos nomes de métrica da spec.**
A spec cita `http_requests_total`. **Essa métrica não existe no .NET 8.** Ver [fase 04](04-metricas-otel-collector-prometheus.md) — o primeiro passo lá é ler `/metrics` e anotar os nomes reais.

**5. Observabilidade não pode virar dependência dura.**
Se o Loki cair, a API continua respondendo. Se o Tempo cair, idem. Toda integração aqui é *fire and forget* com buffer. Se em algum momento uma exceção de exportação estourar na requisição, o desenho está errado.

---

## 8. Checklist final de aceite

A trilha está concluída quando **todos** estiverem marcados:

- [ ] `docker compose --profile obs up -d` sobe a stack inteira
- [ ] `docker compose up -d` continua subindo só `api` + `web` (nada quebrou)
- [x] Health checks respondendo em `/health`, `/health/live`, `/health/ready`
- [ ] Logs estruturados saindo da API
- [ ] Logs pesquisáveis no Grafana via Loki
- [ ] Métricas expostas pelo Collector e coletadas pelo Prometheus (target `UP`)
- [ ] Métricas visíveis no Grafana
- [ ] Traces chegando no Tempo
- [ ] Traces visíveis e navegáveis no Grafana
- [ ] TraceId de um log abre o trace correspondente (e vice-versa)
- [ ] Dashboard `ASP.NET Core Observability` provisionado e sobrevivendo a `down`/`up`
- [ ] 3 alertas configurados e disparando de verdade
- [x] Cenários de erro reproduzíveis por query string
- [x] Cenários de latência reproduzíveis por query string
- [ ] `docs/OBSERVABILIDADE.md` explicando o laboratório inteiro
- [ ] Ambiente destruído com `down -v` e recriado do zero sem intervenção manual

---

## 9. Relação com o roadmap principal

Esta trilha é **paralela** às fases 3 e 4 do [`roadmap.md`](../roadmap.md) e não bloqueia nenhuma delas. Ela absorve dois itens que estavam na Fase 4:

- ~~Serilog estruturado + correlation id~~ → fases [03](03-logs-serilog-loki.md) e [06](06-correlacao-traceid-logs-traces.md)
- ~~Health checks (`/health`) incluindo o SQL Server~~ → fase [01](01-health-checks.md)

Continuam na Fase 4 do roadmap: notificações de vencimento, toggle de tema e testes automatizados.
