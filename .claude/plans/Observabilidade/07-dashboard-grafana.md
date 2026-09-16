# Fase 07 — Dashboard consolidado no Grafana

> ⬅️ anterior: [06 — Correlação](06-correlacao-traceid-logs-traces.md) · ➡️ próxima: [08 — Alertas](08-alertas.md)
> **Containers novos:** nenhum. Provisioning, JSON — e uma métrica nova na aplicação (ver [Resultado da execução](#resultado-da-execucao)).

---

## Objetivo pedagógico

Uma tela que responde, em cinco segundos: **"minha aplicação está saudável agora?"**

E aprender que dashboard bom não é o que mostra mais números — é o que faz a anomalia **saltar aos olhos** e oferece o próximo clique. Painel que ninguém consegue interpretar sob pressão é painel decorativo.

---

## O que entra no projeto

**Arquivos novos:**

```text
docker/grafana/provisioning/dashboards/dashboards.yml
docker/grafana/dashboards/corefinance-observability.json
```

O volume `./docker/grafana/dashboards:/var/lib/grafana/dashboards:ro` já foi montado na [fase 03](03-logs-serilog-loki.md).

---

## Passos

### 1. Provider de dashboards

```yaml
# docker/grafana/provisioning/dashboards/dashboards.yml
apiVersion: 1
providers:
  - name: CoreFinance
    orgId: 1
    folder: CoreFinance
    type: file
    disableDeletion: false
    updateIntervalSeconds: 30
    allowUiUpdates: true
    options:
      path: /var/lib/grafana/dashboards
      foldersFromFilesStructure: false
```

`allowUiUpdates: true` deixa você editar pela UI **sem** o Grafana reverter a cada 30 s — importante para o fluxo de trabalho do passo 3. Mas atenção: edição pela UI **não** volta para o arquivo. Ela se perde no próximo `down -v`.

### 2. Estrutura do dashboard `ASP.NET Core Observability`

Quatro faixas, em ordem de "o que eu olho primeiro":

**Faixa 1 — Estado agora** (stat panels, altura pequena, com limiares coloridos)

| Painel | Query | Limiares |
|---|---|---|
| Requests/s | `sum(rate(http_server_request_duration_seconds_count[5m]))` | — |
| Error rate | taxa de erro × 100, unidade `percent` | verde <1, amarelo <5, vermelho ≥5 |
| P95 latency | `histogram_quantile(0.95, sum by (le) (rate(...bucket[5m])))`, unidade `s` | verde <0,5, amarelo <1, vermelho ≥1 |
| Health | `corefinance_health_status` | *value mappings*: 1→`HEALTHY` verde, 0.5→`DEGRADED` amarelo, 0→`UNHEALTHY` vermelho |

**Faixa 2 — Tráfego**

- *Request rate* — série temporal, `sum by (http_route) (rate(...count[5m]))`, legenda por rota. Mostra **qual** endpoint carrega o sistema.
- *Requests por status* — `sum by (http_response_status_code) (rate(...count[5m]))`, empilhado. 2xx/4xx/5xx com cores fixas (override por valor).

**Faixa 3 — Latência**

- *Latência: média vs P95 vs P99* — três séries no mesmo gráfico. **É o painel mais didático do dashboard**: quando a distância entre média e P95 se abre, existe uma cauda de requisições lentas que a média esconde.
- *Latência por rota (P95)* — `histogram_quantile(0.95, sum by (le, http_route) (rate(...bucket[5m])))`.

**Faixa 4 — Runtime e Logs**

- CPU, memória gerenciada, GC por geração, threads do pool — nomes conforme anotado na [fase 04](04-metricas-otel-collector-prometheus.md) (variam com a versão da `Instrumentation.Runtime`; **não copie de tutorial**).
- *Logs recentes* — painel tipo **Logs**, datasource Loki, query `{app="corefinance-api"} | json` com filtro por nível na variável de template. Com os derived fields da [fase 06](06-correlacao-traceid-logs-traces.md), cada linha aqui já tem o botão para o trace: o dashboard vira **ponto de partida da investigação**, não só de leitura.

### 3. Fluxo de trabalho recomendado

> 💡 **Não escreva o JSON à mão.** É onde a maioria desiste. Faça assim:
> 1. Monte os painéis na UI do Grafana (`localhost:3001`), com dados reais rodando `gerar-carga.ps1` ao fundo — construir painel sem dado é adivinhação.
> 2. **Dashboard settings → JSON Model** (ou *Export → Save to file*).
> 3. Salve em `docker/grafana/dashboards/corefinance-observability.json`.
> 4. Ao exportar, **remova o campo `id`** e fixe um `uid` estável (ex.: `corefinance-obs`) — `id` é da instância e causa conflito no provisioning.
> 5. `docker compose --profile obs restart grafana` e confirme que o dashboard voltou do arquivo.

### 4. Variáveis de template

```text
$env       — label_values(env)              → reaproveitável em outros ambientes
$route     — label_values(http_server_request_duration_seconds_count, http_route)
$intervalo — intervalo customizado (1m, 5m, 15m, 1h) usado nos rate()
```

Usar `rate(...[$intervalo])` em vez de `[5m]` fixo ensina, na prática, como a janela muda a leitura: janela curta é reativa e ruidosa; janela longa é suave e atrasada.

---

## Como validar

```powershell
docker compose --profile obs up -d
.\scripts\gerar-carga.ps1 -Cenario misto -Duracao 180
```

1. `http://localhost:3001` → pasta **CoreFinance** → dashboard aparece **sem** ter sido importado à mão.
2. Todos os painéis com dados (nenhum "No data").
3. `.\scripts\gerar-carga.ps1 -Cenario erro` → o stat de *Error rate* fica vermelho em menos de 1 min.
4. `-Cenario lento -Delay 3000` → **P95 sobe muito mais que a média** no painel da faixa 3.
5. `docker stop sqlserver_container` → *Health* vira `UNHEALTHY`.
6. Um log de erro no painel de logs → clicar no TraceID → o trace abre.
7. **Teste do provisioning:**
   ```powershell
   docker compose --profile obs down
   docker compose --profile obs up -d
   ```
   O dashboard continua lá, com todos os painéis. Se sumiu, o provisioning está errado — e era exatamente isso que a seção 14 da spec queria evitar.

---

## Dicas e armadilhas

> 💡 **Cinco segundos, não cinco minutos.** A faixa 1 responde sozinha "estou bem?". As faixas seguintes existem para o "por quê". Se você precisa rolar a tela para saber se está tudo bem, a ordem está errada.

> 💡 **Limiar sem cor é número; com cor é informação.** Configure *thresholds* em todo stat panel. "P95 = 0,8 s" não diz nada a quem não conhece o sistema; "P95 amarelo" diz.

> ⚠️ **Cuidado com `No data` vs zero.** Painel vazio pode significar "nenhum erro" (ótimo) ou "o Prometheus parou de coletar" (péssimo). Nas opções do painel, configure *No value* e considere um painel dedicado a `up{job="otel-collector"}` — **monitorar o monitor** é parte do trabalho.

> ⚠️ **`id` vs `uid` no JSON.** `uid` é seu, estável, e é o que aparece na URL. `id` é interno da instância — exportar com `id` preenchido causa conflito silencioso no provisioning e o dashboard não atualiza.

> 💡 **Legendas curtas.** `{{http_route}}` em vez da série inteira. Legenda de três linhas rouba metade do gráfico.

> 💡 **Anote o `refresh` do dashboard.** 5 s parece ótimo e é um jeito silencioso de martelar o Prometheus. 30 s ou 1 min basta para tudo que não é incidente ativo.

---

## Conceitos aprendidos

- **Golden signals** (tráfego, erros, latência, saturação) e por que são esses quatro.
- Hierarquia visual: estado → tendência → detalhe.
- **Dashboard as code** e por que provisioning derrota "eu configuro depois".
- Percentil vs média, agora visualmente.
- Dashboard como **porta de entrada da investigação**, ligado a logs e traces.

---

## Critério de aceite

- [x] Dashboard `ASP.NET Core Observability` aparece provisionado, na pasta CoreFinance
- [x] Faixa 1 responde "estou saudável?" sem rolar a tela
- [x] Requests, erros, latência (média/P95/P99), runtime e logs — todos com dados
- [x] Limiares coloridos configurados nos stats
- [x] O painel de logs abre o trace pelo TraceID
- [x] Sobrevive a `down` + `up` sem intervenção manual
- [x] JSON versionado no repositório, com `uid` fixo e sem `id`

---

<a id="resultado-da-execucao"></a>

## Resultado da execução (2026-09-16)

### 1. A faixa 4 pediu uma métrica que não existia

O plano lista **CPU** na faixa 4. Ela não estava no inventário da [fase 04](04-metricas-otel-collector-prometheus.md#resultado-da-execucao) por um motivo simples: a `OpenTelemetry.Instrumentation.Runtime` **não publica CPU**. Quem publicaria é a `OpenTelemetry.Instrumentation.Process` — e ela nunca saiu de pré-release (a lista do NuGet vai de `0.1.0-alpha.1` a `1.18.0-rc.1`, sem uma única versão estável). Como este projeto só usa OTel estável, o painel teria que ser cortado ou a métrica teria que ser nossa.

Ela virou nossa, em `AppMetrics.cs`, e a conta é a própria definição de utilização:

```text
tempo de CPU consumido no intervalo / (tempo de relógio × núcleos disponíveis)
```

O que isso ensina, e que um pacote pronto teria escondido: **utilização é sempre uma média sobre um intervalo**. Não existe "uso de CPU agora" — existe "uso de CPU entre duas coletas" (aqui, os 15 s do `OTEL_METRIC_EXPORT_INTERVAL`). O `Environment.ProcessorCount` respeita o limite de CPU do container, então `1.0` significa "saturei o que me deram", não "saturei a máquina".

**Conferência contra o `docker stats`**, no cenário `erro` a 1441 req/s:

| Fonte | Leitura |
|---|---|
| `docker stats` | `CPU=343.03%` → 3,43 núcleos |
| nossa métrica | `max_over_time(...[3m])` = `0,2537` × 12 núcleos = 3,04 núcleos |

Diferença compatível com janelas de amostragem diferentes — a métrica está certa.

### 2. O nome exportado não foi o nome registrado

Registrado no código como `corefinance.process.cpu.utilization` com unidade `"1"`. No `/metrics` do Collector:

```text
corefinance_process_cpu_utilization_ratio
```

O exportador **acrescentou `_ratio`** por causa da unidade `1` — do mesmo jeito que `{status}` e `{payment}` são anotações e somem. É a regra de ouro da fase 04 cobrando de novo: o nome se lê do `/metrics`, não se deduz do código.

### 3. Faixa 4 reorganizada

Cinco gráficos não cabem em uma linha de 24 colunas sem ficarem estreitos demais. A faixa virou duas linhas antes do painel de logs:

```text
y=24   CPU (8) | Memória gerenciada (8) | GC por geração (8)
y=31   Thread pool (12) | Exceções e contenção (12)
y=38   Logs recentes (24)
```

### 4. As sete validações

| # | Validação | Resultado |
|---|---|---|
| 1 | Dashboard provisionado na pasta CoreFinance | `/api/search` → `folderTitle: CoreFinance`, `uid: corefinance-obs` |
| 2 | Nenhum painel sem dado | **21 alvos de 15 painéis, todos com série** (Prometheus + Loki consultados direto) |
| 3 | Cenário `erro` deixa o stat vermelho | 129.674 req em 90 s, 100 % HTTP 500 → taxa de erro `100` no `[1m]`, acima do limiar vermelho (5) |
| 4 | Cenário `lento` abre P95 vs média | ver tabela abaixo |
| 5 | SQL parado → `UNHEALTHY` | `corefinance_health_status = 0` e `health_check="sqlserver" = 0` (com `self = 1`); depois do `docker start`, os dois voltam a `1` |
| 6 | Log de erro → TraceID → trace | regex do derived field casou na linha `level="error"`; o TraceID `325a11dd…c52399` devolveu no Tempo um span `GET api/Demo/random` com `STATUS_CODE_ERROR` |
| 7 | `down` + `up` | dashboard de volta sozinho, 15 painéis, 4 variáveis, `provisionedExternalId: corefinance-observability.json` |

**Validação 4, o painel mais didático da fase, com números.** O cenário `lento` foi rodado *junto* com o `misto` — poucas requisições de 3 s no meio de muitas rápidas, que é o caso real:

| Janela `[2m]` | Média | P95 | P99 | P95/média |
|---|---|---|---|---|
| só `misto` (28 req/s) | 133 ms | 480 ms | 2.122 ms | 3,6× |
| `misto` + `lento` (1,3 req/s de 3 s) | 276 ms | 2.346 ms | 4.453 ms | **8,5×** |

1,3 req/s lentas em 28 req/s **dobraram a média e quintuplicaram o P95**. É a demonstração de que a média esconde a cauda — e ela está visível no gráfico da faixa 3 sem precisar de explicação.

### 5. Duas surpresas que ficam registradas

> ⚠️ **`provisioned: false` num dashboard provisionado.** O `/api/dashboards/uid/...` devolve `meta.provisioned = false` mesmo com o dashboard vindo do arquivo. É efeito do `allowUiUpdates: true`: o Grafana destrava a edição pela UI e, com isso, deixa de marcar o dashboard como provisionado. Quem confirma a origem é o `meta.provisionedExternalId`, que continua apontando para o `corefinance-observability.json`. Procurar pela flag errada faz parecer que o provisioning falhou.

> ⚠️ **Depois de um `down`/`up`, os painéis de runtime desenham duas linhas.** As métricas de runtime e de saúde são consultadas sem agregação, então há **uma série por `service_instance_id`** — e o `service.instance.id` muda a cada partida da API. Durante os ~5 min em que a instância antiga ainda está dentro da janela de consulta, convivem as duas. Some sozinho. Vale saber por quê: o stat de *Saúde* nesse intervalo mostra dois valores, não um.
