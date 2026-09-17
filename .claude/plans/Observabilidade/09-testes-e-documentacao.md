# Fase 09 — Testes de observabilidade e documentação

> ⬅️ anterior: [08 — Alertas](08-alertas.md) · ➡️ próxima: [10 — Extras](10-extras-e-proximos-passos.md)
> **Containers novos:** nenhum. É a fase de fechamento: provar que tudo funciona e deixar registrado.

---

## Objetivo pedagógico

Duas coisas que costumam ficar de fora e são justamente o que transforma um experimento em conhecimento:

1. **Validar cada sinal deliberadamente.** Não "parece que está funcionando" — um roteiro com comando e resultado esperado.
2. **Documentar para o você de daqui a seis meses**, que não vai lembrar por que o Grafana está na 3001 nem qual query mostra o P95.

---

## O que entra no projeto

**Arquivos novos:**

```text
docs/OBSERVABILIDADE.md
```

**Arquivos alterados:**

```text
README.md (se existir) ou docs/ARCHITECTURE.md   ← ponteiro para o novo doc
.claude/plans/Observabilidade/00-visao-geral.md   ← marcar o checklist final
```

---

## Parte 1 — Os 7 testes (seção 19 da spec)

Cada teste: **comando** → **resultado esperado** → **o que você aprende se falhar**.

### Teste 1 — Health

```powershell
curl http://localhost:5176/health
curl http://localhost:5176/health/live
curl http://localhost:5176/health/ready
```

✅ JSON estruturado, `status: Healthy`, sem token. No Grafana, o stat *Health* mostra `HEALTHY`.
❌ Se `/health` pedir 401, ele está dentro de um controller com `[Authorize]`.

### Teste 2 — Logs

```powershell
curl http://localhost:5176/api/demo/error
```

Grafana → Explore → Loki → `{app="corefinance-api"} | json | level="Error"`
✅ O erro aparece em segundos, com stack trace, `TraceId` e `CorrelationId`.
❌ Se não aparecer: ative `Serilog.Debugging.SelfLog` — o sink falha em silêncio por design.

### Teste 3 — Métricas

```powershell
.\scripts\gerar-carga.ps1 -Cenario sucesso -Duracao 60
```

✅ `sum(rate(http_server_request_duration_seconds_count[5m]))` sobe; o painel *Request rate* acompanha.
❌ Target `DOWN` em `:9090/targets` → o Collector não está expondo ou está fora da rede.

### Teste 4 — Latência

```powershell
.\scripts\gerar-carga.ps1 -Cenario lento -Delay 3000 -Duracao 120
```

✅ **P95 dispara, média sobe pouco.** Registre os dois números lado a lado — é a lição mais transferível de toda a trilha.

### Teste 5 — Tracing

```powershell
curl "http://localhost:5176/api/demo/slow?delay=3000"
```

Grafana → Explore → Tempo → `{ duration > 2s }`
✅ O trace abre com os spans e a duração de cada um.

### Teste 6 — Correlação

```powershell
curl -i -H "X-Correlation-Id: teste-006" http://localhost:5176/api/demo/error
```

✅ Do log ao trace (botão *Ver trace*) e do span aos logs (*Logs for this span*), nos dois sentidos.
❌ Se o botão não aparece: `matcherRegex` não casa com o formato do log, ou faltou o `$$` no `url`.

### Teste 7 — Alertas

```powershell
.\scripts\gerar-carga.ps1 -Cenario erro -Duracao 180
```

✅ A regra percorre `Normal` → `Pending` → `Alerting` e volta a `Normal` sozinha ao fim da carga.

---

## Parte 2 — Os 5 cenários de falha (seção 20)

| Cenário | Como provocar | O que observar |
|---|---|---|
| **HTTP 500** | `/api/demo/error` | métrica de erro ↑, log `Error`, trace vermelho, alerta |
| **Latência alta** | `/api/demo/slow?delay=5000` | P95 ↑↑, média ↑, span longo no trace |
| **Dependência indisponível** | `docker stop sqlserver_container` | `/health/ready` Unhealthy, `corefinance_health_status`=0, erros de SQL nos logs e spans de SQL falhando |
| **Health unhealthy** | idem acima | alerta *Application Unhealthy*, stat vermelho |
| **Alto volume** | `gerar-carga.ps1 -Cenario volume -Paralelo 50` | RPS ↑, métricas de runtime (thread pool, GC) reagindo, latência subindo por saturação |

> 💡 O cenário de **dependência indisponível** é o mais rico: ele acende **os três sinais ao mesmo tempo** e é o melhor exercício de leitura conjunta. Faça-o por último e com calma — tente diagnosticar olhando primeiro só o dashboard, depois só o trace, depois só os logs, e compare o que cada caminho te contou.

> 💡 **Registre os números.** Anote no `docs/OBSERVABILIDADE.md` os valores observados em cada teste (RPS, P95, taxa de erro). Vira sua **linha de base**: sem ela, "está lento" é opinião.

---

## Parte 3 — `docs/OBSERVABILIDADE.md`

Os 12 pontos da seção 23, na ordem em que alguém realmente lê:

1. **Objetivo** — o que este laboratório é e por que existe dentro do CoreFinance
2. **Arquitetura** — o diagrama de [00](00-visao-geral.md)
3. **Componentes** — a tabela "o que cada ferramenta resolve"
4. **Como subir** — `docker compose --profile obs up -d` (e por que o profile existe)
5. **URLs de acesso** — a tabela de portas, com o aviso de que **Grafana é 3001, não 3000**
6. **Como testar logs** — a query LogQL pronta para copiar
7. **Como testar métricas** — as consultas PromQL prontas
8. **Como testar traces** — as queries TraceQL prontas
9. **Como testar health checks**
10. **Como testar alertas**
11. **Como correlacionar Trace ID e logs** — o fluxo dos dois sentidos
12. **Como desligar** — `docker compose --profile obs down` (mantém dados) vs `down -v` (apaga tudo)

Mais duas seções que valem ouro depois:

- **Troubleshooting** — "não aparece log/métrica/trace", com a sequência de checagem de cada fase.
- **Decisões e trade-offs** — por que Collector, por que Serilog direto para o Loki, por que profile opt-in. O *porquê* é o que se perde primeiro.

---

## Parte 4 — Teste de reprodutibilidade

O aceite final da spec (seção 25): o ambiente pode ser destruído e recriado.

```powershell
docker compose --profile obs down -v      # apaga inclusive os volumes
docker compose --profile obs up -d --build
.\scripts\gerar-carga.ps1 -Duracao 120
```

Do zero, **sem um único clique manual**, deve voltar:

- [ ] os 4 datasources conectados
- [ ] o dashboard na pasta CoreFinance
- [ ] as 3 regras de alerta
- [ ] logs, métricas e traces fluindo
- [ ] a correlação funcionando nos dois sentidos

Se qualquer item exigir configuração manual, **há um provisioning faltando** — corrija antes de considerar a trilha concluída.

E o teste que protege o dia a dia:

```powershell
docker compose --profile obs down
docker compose up -d          # só api + web, como sempre foi
curl http://localhost:5176/health/live
curl http://localhost:3000
```

✅ A aplicação funciona normalmente sem nenhuma peça de observabilidade no ar. Se ela não subir, ou ficar lenta, ou logar erro a cada requisição, **a observabilidade virou dependência dura** — o que é justamente o pecado que toda esta arquitetura tentou evitar.

---

## Dicas e armadilhas

> 💡 **Documente o que te surpreendeu.** Os nomes reais das métricas, o schema v13 do Loki, o UID do Tempo, o `$$` do derived field. Isso não está em nenhum tutorial e é o que você vai reprocurar.

> 💡 **Print dos painéis com dados** dentro do `docs/OBSERVABILIDADE.md`, se puder. Um dashboard vazio daqui a seis meses não conta a mesma história.

> ⚠️ **`down -v` apaga os volumes.** É de propósito neste teste, mas cuidado para não confundir com o `down` do dia a dia. Vale deixar essa distinção explícita na documentação.

> 💡 **A última pergunta é a melhor.** Depois de tudo pronto, pergunte-se: *se um usuário reclamar amanhã "o dashboard financeiro está lento", quantos cliques até eu saber a causa?* Se a resposta for "muitos", falta um painel, um link ou uma anotação — e agora você sabe exatamente onde acrescentar.

---

## Conceitos aprendidos

- **Testar a observabilidade** como se testa qualquer outra funcionalidade.
- **Linha de base**: sem número de referência, não existe "anormal".
- **Reprodutibilidade** e infraestrutura como código.
- Observabilidade como preocupação **transversal e opcional** — nunca dependência dura.
- Documentar **decisão e porquê**, não só passo a passo.

---

## Resultado da execução (2026-09-17)

### 1. Os 7 testes, com número

| # | Teste | Resultado |
|---|---|---|
| 1 | Health | `Healthy` nos três endpoints, HTTP 200, sem token. `/health` em **6,3 ms** (`self` 0,0003 ms, `sqlserver` 3,0 ms) |
| 2 | Logs | duas linhas `level="error"` no Loki em segundos, com stack trace apontando `DemoController.cs:line 57`, `TraceId`, `CorrelationId=fase09-teste02` e `StatusCode=500` |
| 3 | Métricas | `volume`, 60 s, 8 em paralelo: **120.972 req**, 100 % HTTP 200, **2.016 req/s**. Target `otel-collector:8889` `up` |
| 4 | Latência | `misto` + `lento` simultâneos: **média 235 ms × P95 1,84 s × P99 4,22 s** |
| 5 | Tracing | `{ duration > 2s }` devolveu 3 traces `GET api/Demo/slow` de 3.000–3.001 ms |
| 6 | Correlação | `TraceId` do log abriu o trace no Tempo; derived field e `tracesToLogsV2` conferidos no datasource |
| 7 | Alertas | `inactive` → `pending` (11:09:36) → `firing` (11:10:37) → `inactive` sozinho (11:17:14) |

### 2. O teste 4 precisou de dois geradores ao mesmo tempo

O enunciado do teste manda rodar só `-Cenario lento` e reparar que "o P95 dispara e a média sobe
pouco". **Com o cenário `lento` puro isso não acontece** — se toda requisição leva 3 s, média e
P95 coincidem (medido no cliente: média 3.006 ms, P95 3.010 ms). É a mesma ressalva registrada na
[fase 08](08-alertas.md#resultado-da-execucao), agora com o teste refeito do jeito que prova o
ponto: `misto` e `lento` em paralelo por 240 s.

| Medida | Valor |
|---|---|
| Requisições/s | 31,0 |
| **Média** | **235 ms** |
| **P95** | **1,84 s** |
| P99 | 4,22 s |
| Taxa de erro | 4,7 % |

**1,3 req/s levando 3 s empurraram o P95 para quase 2 s com a média em 235 ms.** É o usuário que
reclama e o gráfico que não mostra.

> ⚠️ **Rajada curta esconde o incidente no `rate([5m])`.** Logo depois dos 60 s do teste de volume,
> o P95 global marcava **4,7 ms** mesmo com 160 requisições de 3 s já contabilizadas: 120.972
> requisições rápidas na mesma janela diluíram tudo. Quebrando por rota, o número aparecia na hora
> — `api/Demo/slow` = **4,875 s** contra `api/Demo/success` = 4,7 ms. *Quando o agregado não
> mostra, quebre por dimensão.*

> 💡 **4,875 s para requisições de 3 s não é erro de medida, é o bucket.** `histogram_quantile`
> interpola linearmente dentro do bucket em que o percentil cai — com 3 s dentro da faixa
> `(2,5 s – 5 s]`, sai 4,875 s. Histograma responde faixa, não valor exato; para o valor exato,
> o trace.

### 3. O cenário de dependência fora do ar acendeu os três alertas

Foi o mais rico, como o plano previa — e mais do que o previsto: **um único `docker stop` levou as
três regras a `firing`**.

| Horário | Evento |
|---|---|
| 11:17:48 | `docker stop sqlserver_container` |
| 11:17:49 | `/health/ready` → **503**, com a mensagem do TCP Provider no JSON; `/health/live` segue `Healthy` |
| 11:19:29 | `POST /api/auth/login` → **500 em 14.587 ms** (timeout de conexão) |
| 11:19:35 | `corefinance_health_status` = 0 · **Application Unhealthy** `firing` |
| 11:21:08 | **High Error Rate** `firing` |
| 11:22:09 | **High Latency** `firing` |
| 11:22:17 | `docker start sqlserver_container` |
| 11:22:55 | métrica de saúde de volta a 1 (38 s) |
| 11:23:10 | **Application Unhealthy** → `Normal` (53 s) |
| 11:25:38 | os outros dois → `Normal`, sozinhos (a janela `[5m]` esvaziando) |

> ⚠️ **Dependência fora do ar não produz span de SQL.** A falha acontece ao **abrir a conexão**, e
> a instrumentação do `SqlClient` só cria span para comando executado. O trace de `POST api/Auth/login`
> tem **um span só**, de 14.591 ms e `STATUS_CODE_ERROR`, com a mensagem do SQL Server no status.
> Quem procura o filho vermelho na árvore para provar que o banco caiu não acha nada e conclui a
> coisa errada.

> 💡 **Dependência morta vira incidente de latência, não só de erro.** Um login de ~100 ms virou
> 14,6 s por causa do timeout de conexão — e foi isso, sem carga nenhuma rodando, que disparou o
> alerta de latência. Ler o incidente só pela taxa de erro teria contado metade da história.

### 4. Reprodutibilidade: passou

`down -v` (os quatro volumes removidos) + `up -d --build` + `gerar-carga.ps1 -Duracao 120`.
Voltaram sozinhos: os 3 datasources, o dashboard `corefinance-obs` na pasta `CoreFinance`, as 3
regras, o contact point `lab-local`, o target `up` no Prometheus e os três sinais fluindo
(1.542 linhas de log em 2 min · 19,9 req/s · P95 772 ms · taxa de erro 5,03 % · traces com
`{ status = error }`). A correlação log → trace foi conferida de novo, com `STATUS_CODE_ERROR`.

E sem observabilidade nenhuma (`down` do profile + `docker compose up -d`): API **saudável em 6 s**,
`/health/live` em 2,3 ms, web em 72 ms, `/api/demo/success` entre 2 e 35 ms. O único efeito é ruído
no console do sink do Loki (`Name or service not known (loki:3100)`), que **não afeta requisição
nenhuma**. O objetivo da trilha inteira — observabilidade como preocupação opcional — se sustentou.

### 5. Duas correções ao enunciado do plano

> ⚠️ **`-Cenario sucesso` não existe.** Os cenários do `gerar-carga.ps1` são `misto`, `erro`,
> `lento` e `volume`. O equivalente a "sucesso" é `volume`, que bate em `/api/demo/success`.

> ⚠️ **"Os 4 datasources" são 3.** Loki, Prometheus e Tempo. Não existe um quarto nesta
> implementação — o número veio da spec original.

### 6. O que ficou de fora

**Print dos painéis.** O Grafana deste lab não tem o plugin `grafana-image-renderer`, então não há
como exportar PNG do dashboard por linha de comando. A validação dos painéis com dado real está na
[fase 07](07-dashboard-grafana.md#resultado-da-execucao) (21 alvos de 15 painéis, todos com série).

---

## Critério de aceite

- [x] Os 7 testes executados e com resultado registrado
- [x] Os 5 cenários de falha reproduzidos e observados
- [x] `docs/OBSERVABILIDADE.md` completo, com os 12 pontos + troubleshooting + decisões
- [x] `down -v` seguido de `up` recria tudo sem intervenção manual
- [x] `docker compose up -d` sem o profile continua funcionando normalmente
- [x] Checklist final de [00 — Visão geral](00-visao-geral.md) totalmente marcado
