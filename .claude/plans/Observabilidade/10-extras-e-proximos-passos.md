# Fase 10 — Extras e próximos passos

> ⬅️ anterior: [09 — Testes e documentação](09-testes-e-documentacao.md) · 🏠 [Visão geral](00-visao-geral.md)
> **Tudo aqui é opcional.** A trilha está completa sem esta página. Cada item é independente — escolha por curiosidade, não por checklist.

---

## Por que existe uma página de extras

A spec avisa (seção 21): *evitar overengineering; é um laboratório de aprendizado, não uma plataforma de produção*. As fases 01–09 respondem às 10 perguntas do objetivo. O que está aqui vai **além** delas — e por isso fica separado, para não inchar o escopo principal.

Ordenado por **relação custo/aprendizado**: os primeiros dão mais entendimento por hora investida.

---

## 1. Repassar o `traceparent` no proxy do Next.js ⭐

**Custo:** ~15 minutos. **Ganho:** entender propagação de contexto de verdade.

Hoje o trace começa na API. O proxy em `web/src/app/api/[...path]/route.ts` já repassa o `authorization` — repassar também `traceparent` e `X-Correlation-Id` faria o contexto atravessar o front.

Depois disso, gere um `traceparent` na mão e veja seu span nascer filho de um trace que você inventou. É o exercício mais barato para entender por que o padrão W3C existe.

---

## 2. Grafana Alloy: logs de todos os containers ⭐

**Custo:** 1 container + um `config.alloy`. **Ganho:** o outro modelo de coleta de logs.

Na [fase 03](03-logs-serilog-loki.md) o Serilog empurra logs direto para o Loki. O modelo alternativo é um **agente** lendo o stdout dos containers e enviando para o Loki — assim `web` (Next.js) e `sqlserver_container` entram no mesmo Grafana **sem tocar no código deles**.

```text
Serilog → Loki     : controle total dos campos, exige código na aplicação
Alloy   → Loki     : zero código, funciona para qualquer container, exige parsing no pipeline
```

Rodar os dois em paralelo e comparar é um exercício excelente. Cuidado para não duplicar os logs da API (filtre o container `corefinance-api` no Alloy, ou desligue o sink).

---

## 3. Logs pelo Collector (pipeline `logs`)

**Custo:** trocar o sink por `OpenTelemetry.Appender.Serilog` + pipeline no Collector. **Ganho:** os três sinais por um caminho só.

O desenho "tudo OTLP" é o que mais se vê em produção: uma configuração, um ponto de saída, um lugar para aplicar redaction e sampling. O contraste com o sink direto (mais simples de depurar, menos peças) é a lição.

---

## 4. Exemplars: do gráfico de latência direto para o trace ⭐

**Custo:** ativar `--enable-feature=exemplar-storage` no Prometheus + `exemplars` no Collector. **Ganho:** o clique que impressiona.

Exemplar é um Trace ID anexado a um ponto do histograma. Na prática: você vê o pico de latência no gráfico, **clica no ponto** e cai no trace daquela requisição específica. É a correlação da [fase 06](06-correlacao-traceid-logs-traces.md) levada ao gráfico de métrica.

---

## 5. Span metrics e service graph no Tempo

**Custo:** habilitar `metrics_generator` no `tempo.yml` + `remote_write` para o Prometheus. **Ganho:** métricas RED derivadas automaticamente dos traces.

O Tempo passa a gerar taxa/erro/duração por serviço e operação a partir dos spans — e desenha o grafo de dependências. Com um serviço só o grafo é modesto, mas ensina de onde vem a métrica automática das plataformas de APM.

⚠️ Requer o `remote_write` habilitado no Prometheus.

---

## 6. k6 como container de carga

**Custo:** 1 container no profile `obs` + um script JS. **Ganho:** carga realista e repetível.

O `gerar-carga.ps1` da [fase 02](02-endpoints-de-demonstracao.md) resolve o lab, mas não faz rampa, degrau, patamar nem relatório. O k6 faz — e o próprio k6 exporta métricas OTLP, então **a ferramenta de teste também aparece no seu Grafana**.

---

## 7. OpenTelemetry no front (Next.js)

**Custo:** alto (SDK de browser, CORS, endpoint OTLP exposto). **Ganho:** trace ponta a ponta, do clique ao SQL.

O trace começaria no navegador (Core Web Vitals, tempo de fetch), atravessaria o proxy e chegaria ao SQL Server. É o passo natural depois do item 1, e o mais próximo de RUM (Real User Monitoring).

---

## 8. Sampling no Collector

**Custo:** um processor. **Ganho:** entender a economia de traces.

Com `tail_sampling` você guarda 100% dos traces com erro ou lentos e uma fração dos normais. É exatamente onde o Collector prova seu valor: **a política muda sem tocar na aplicação**.

Só faz sentido quando o volume incomoda — mas configurar uma vez ensina o conceito.

---

## 9. Alertas mais interessantes

Depois dos três básicos da [fase 08](08-alertas.md):

- **Queda de tráfego** — RPS caiu a zero fora do esperado (a API não está com erro; está *sem ninguém chegando*, e nenhum alerta de erro pegaria isso).
- **Saturação** — thread pool crescendo, GC de gen2 frequente (o quarto *golden signal*, que o dashboard mostra mas ninguém alerta).
- **Erro de negócio** — nenhum pagamento criado em 7 dias, usando `corefinance_payments_created_total`. Alerta sobre *domínio*, não sobre infra — e é o tipo que gera mais valor real.
- **Multi-window burn rate** — a técnica de SLO do Google SRE: janela curta para pegar rápido, janela longa para evitar falso positivo.

---

## 10. SLO e error budget

**Custo:** conceitual. **Ganho:** o vocabulário que conecta observabilidade a decisão de produto.

Definir, por exemplo: *99% das requisições respondem em menos de 500 ms no mês*. A partir daí surgem **error budget** (quanto você ainda pode falhar) e alertas por consumo de orçamento em vez de limiar fixo. É a evolução natural da [fase 08](08-alertas.md) e o assunto que separa "temos alertas" de "sabemos o que é aceitável".

---

## 11. Segurança do laboratório

Coisas que estão relaxadas de propósito e que valem uma passada consciente:

- **Grafana anônimo** (`GF_AUTH_ANONYMOUS_ENABLED=true`) — só porque é local.
- **Loki e Tempo sem autenticação** (`auth_enabled: false`).
- **`SetDbStatementForText = true`** grava SQL nos spans ([fase 05](05-traces-tempo.md)).
- **`UIResponseWriter`** expõe mensagem de exceção no `/health` ([fase 01](01-health-checks.md)).
- **Chave JWT em `appsettings.json` e no `docker-compose.yml`** — pendência que já está no [`roadmap.md`](../roadmap.md) desde a fase 1 e é a mais próxima de virar problema real.

Um exercício honesto: escrever o que mudaria em cada item para ir a produção. Não implementar — **descrever**. O raciocínio é o aprendizado.

---

## 12. Onde isso encosta no roadmap principal

| Item do [`roadmap.md`](../roadmap.md) | Relação |
|---|---|
| Fase 3 — export CSV/Excel/PDF | operação potencialmente lenta: bom candidato a span customizado e métrica própria |
| Fase 4 — notificações via `HostedService` | roda **fora** de requisição: precisa criar a própria `Activity`, senão fica sem trace ([fase 06](06-correlacao-traceid-logs-traces.md)) |
| Fase 4 — testes xUnit + `WebApplicationFactory` | dá para **testar a instrumentação**: asserir que a métrica incrementou, que o span nasceu |
| Fase 4 — chave JWT em secret | item 11 acima |

---

> 💡 **Conselho final:** não faça esta página inteira. Escolha um ou dois itens que respondam a uma pergunta que **você** ficou com vontade de responder ao longo das fases anteriores. Observabilidade aprende-se investigando problema real, não completando lista.
