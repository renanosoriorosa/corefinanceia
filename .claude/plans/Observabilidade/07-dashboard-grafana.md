# Fase 07 — Dashboard consolidado no Grafana

> ⬅️ anterior: [06 — Correlação](06-correlacao-traceid-logs-traces.md) · ➡️ próxima: [08 — Alertas](08-alertas.md)
> **Containers novos:** nenhum. Só provisioning e JSON.

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

- [ ] Dashboard `ASP.NET Core Observability` aparece provisionado, na pasta CoreFinance
- [ ] Faixa 1 responde "estou saudável?" sem rolar a tela
- [ ] Requests, erros, latência (média/P95/P99), runtime e logs — todos com dados
- [ ] Limiares coloridos configurados nos stats
- [ ] O painel de logs abre o trace pelo TraceID
- [ ] Sobrevive a `down` + `up` sem intervenção manual
- [ ] JSON versionado no repositório, com `uid` fixo e sem `id`
