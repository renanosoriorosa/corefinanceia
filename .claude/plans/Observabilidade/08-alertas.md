# Fase 08 — Alertas no Grafana

> ⬅️ anterior: [07 — Dashboard](07-dashboard-grafana.md) · ➡️ próxima: [09 — Testes e documentação](09-testes-e-documentacao.md)
> **Containers novos:** nenhum. Três YAML de provisioning — e um endpoint no `DemoController` para o alerta virar log.

---

## Objetivo pedagógico

Responder à pergunta **10**: *como criar um alerta quando uma condição problemática ocorrer?*

E a virada de chave da trilha inteira: **parar de olhar o dashboard**. Dashboard é para investigar; alerta é para ser avisado. Um sistema que exige alguém olhando a tela não é observável, é vigiado.

---

## O que entra no projeto

**Arquivos novos:**

```text
docker/grafana/provisioning/alerting/contact-points.yml
docker/grafana/provisioning/alerting/notification-policies.yml
docker/grafana/provisioning/alerting/rules.yml
```

O volume de provisioning já está montado desde a [fase 03](03-logs-serilog-loki.md) — o Grafana lê `provisioning/alerting/` automaticamente.

---

## Passos

### 1. Contact point

A spec (seção 16) dispensa Slack e e-mail. Duas opções:

```yaml
# docker/grafana/provisioning/alerting/contact-points.yml
apiVersion: 1
contactPoints:
  - orgId: 1
    name: lab-local
    receivers:
      - uid: lab-webhook
        type: webhook
        settings:
          url: http://api:8080/api/demo/alert-webhook
          httpMethod: POST
```

> 💡 **Dica que fecha o ciclo:** um endpoint `POST /api/demo/alert-webhook` no `DemoController` (fase 02) que apenas **loga** o payload do alerta. O alerta então aparece no Loki, com TraceId, e você acaba **observando a própria observabilidade**. Poucos exercícios fixam melhor a ideia de que tudo é sinal.
>
> Se preferir não criar o endpoint, use `type: webhook` apontando para um receptor descartável, ou simplesmente deixe o contact point padrão — o estado do alerta aparece em **Alerting → Alert rules** de qualquer jeito. Notificação não é requisito; **disparo** é.

### 2. Notification policy

```yaml
apiVersion: 1
policies:
  - orgId: 1
    receiver: lab-local
    group_by: ['alertname']
    group_wait: 10s
    group_interval: 1m
    repeat_interval: 1h
```

`group_wait` curto (10 s) porque num lab você quer feedback rápido; em produção, 30 s–1 min evita tempestade de notificação.

### 3. As três regras

Estrutura de cada regra em `rules.yml` (`apiVersion: 1`, `groups[].rules[]`), com query PromQL → `reduce` → `threshold`:

#### High Error Rate

```promql
sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))
/
clamp_min(sum(rate(http_server_request_duration_seconds_count[5m])), 0.001)
```

- Limiar: `> 0.05` (5%)
- `for: 1m`
- `noDataState: OK` — sem tráfego não é incidente
- Anotações: descrição, e um **link para o dashboard** e para `{ status = error }` no Tempo

> ⚠️ **`clamp_min` no denominador.** Sem tráfego, o denominador é 0 e a divisão vira `NaN` — a regra entra em `NoData` em vez de ficar `Normal`, e o alerta parece "quebrado". Divisão em PromQL sempre pede esse cuidado.

#### High Latency

```promql
histogram_quantile(0.95,
  sum by (le) (rate(http_server_request_duration_seconds_bucket[5m])))
```

- Limiar: `> 1` (segundo)
- `for: 2m` — mais folgado que o de erro: um pico isolado de latência não é incidente, latência sustentada é.
- `noDataState: OK`

#### Application Unhealthy

```promql
corefinance_health_status
```

- Limiar: `< 1`
- `for: 1m`
- `noDataState: Alerting` — **aqui é o inverso**: se a métrica de saúde sumiu, ou a API morreu ou o Collector parou. Ambos merecem alerta.

### 4. Limiares em um lugar só

A spec pede valores facilmente configuráveis. Mantenha os três números no topo do `rules.yml`, com comentário, ou centralize num bloco de anotações:

```yaml
# ── Limiares do laboratório ────────────────────────
#   Error rate  > 5%     por 1 min
#   P95 latency > 1s     por 2 min
#   Health      < 1      por 1 min
```

Mudar limiar deve ser editar uma linha e reiniciar o Grafana — não caçar valor dentro de JSON de regra.

---

## Como validar

```powershell
docker compose --profile obs up -d
```

`http://localhost:3001/alerting/list` → as três regras aparecem provisionadas, estado **Normal**.

**Teste 1 — High Error Rate**

```powershell
.\scripts\gerar-carga.ps1 -Cenario erro -Duracao 180
```

Acompanhe em Alerting → a regra vai `Normal` → **`Pending`** (limiar cruzado, aguardando o `for`) → **`Alerting`** (~1 min depois). Ver a transição por `Pending` é o objetivo pedagógico do teste; não pule essa observação.

**Teste 2 — High Latency**

```powershell
.\scripts\gerar-carga.ps1 -Cenario lento -Delay 2000 -Duracao 240
```

Dispara em ~2 min. Repare que a **latência média pode continuar aceitável** enquanto o P95 estoura — é o alerta pegando o que a média esconderia.

**Teste 3 — Application Unhealthy**

```powershell
docker stop sqlserver_container       # ready fica Unhealthy → métrica cai para 0
# ~1 min depois: Alerting
docker start sqlserver_container      # e volta para Normal sozinho
```

**Teste 4 — resolução.** Pare a carga e confirme que os alertas voltam a `Normal` sem intervenção. Alerta que não se resolve sozinho é pior que alerta nenhum.

**Teste 5 — persistência.** `down` + `up` → as regras continuam lá.

---

## Dicas e armadilhas

> 💡 **Estados: `Normal` → `Pending` → `Alerting` → `Normal`.** O `Pending` é o período `for`: a condição está violada, mas ainda não por tempo suficiente. É o que separa "picozinho" de "problema", e é o campo que mais evita alerta ruidoso.

> ⚠️ **`NoData` merece decisão explícita, regra a regra.** Não existe resposta única: para taxa de erro, sem dado = sem tráfego = OK. Para saúde, sem dado = suspeito = alerta. Deixar no padrão sem pensar é como a maioria dos times acaba com alertas que ninguém entende.

> 💡 **Alerta bom tem quatro propriedades:** é **acionável** (existe algo a fazer), é **específico** (aponta o quê), é **oportuno** (nem cedo demais nem tarde demais) e é **raro** (se dispara toda hora, viram avisos ignorados). Fadiga de alerta é um problema real e mata mais sistemas do que a falta de alerta.

> 💡 **Sintoma, não causa.** Alerte "usuário está recebendo erro" (sintoma) em vez de "CPU a 80%" (causa possível). CPU alta com todo mundo atendido não é incidente; CPU baixa com todo mundo tomando 500 é.

> 💡 **Coloque o próximo passo na anotação.** Um alerta que já traz o link do dashboard e a query do Tempo economiza os cinco minutos mais caros de um incidente. Anotação é onde mora o runbook.

> ⚠️ **Provisioning de alerta é mais chato que o de dashboard.** O formato tem `condition`, `data[]` com `refId`, `relativeTimeRange`, `datasourceUid`… O caminho prático é o mesmo da fase 07: **crie a regra pela UI, exporte em Alerting → Export → YAML**, e versione o resultado.

> ⚠️ **`datasourceUid` precisa bater** com o `uid` definido em `datasources.yml` (`prometheus`, `loki`, `tempo`). Uid errado = regra que nunca avalia, sem erro visível.

---

## Conceitos aprendidos

- Regra de alerta = **query + limiar + duração + estado de exceção**.
- `Pending`/`for` como filtro de ruído.
- Tratamento de `NoData` como decisão de projeto, não configuração padrão.
- **Fadiga de alerta** e alerta baseado em sintoma.
- **Alerting as code** com provisioning.
- Fechar o ciclo: o alerta vira log, que é observável como qualquer outro sinal.

---

## Critério de aceite

- [x] Três regras provisionadas e visíveis em Alerting → Alert rules
- [x] High Error Rate dispara com `-Cenario erro` e a transição por `Pending` foi observada
- [x] High Latency dispara com `-Cenario lento`
- [x] Application Unhealthy dispara com o SQL Server parado
- [x] Todos voltam a `Normal` sozinhos quando a condição termina
- [x] Limiares centralizados e fáceis de alterar
- [x] Regras sobrevivem a `down` + `up`

---

<a id="resultado-da-execucao"></a>

## Resultado da execução (2026-09-16)

### 1. O endpoint foi criado — e é a melhor parte da fase

A "dica que fecha o ciclo" virou `POST /api/demo/alert-webhook` (`DemoController`) mais o DTO
`AlertaWebhookRequest`, um recorte do payload do Grafana com o que realmente se usa: `status`,
`labels`, `annotations` e `values`.

Duas decisões que valem o comentário:

- **`firing` loga em `Warning`, `resolved` loga em `Information`.** Como `level` é a única
  propriedade promovida a label do Loki (fase 03), `{app="corefinance-api", level="warning"}`
  passa a listar os alertas que dispararam sem precisar ler o texto da linha.
- **O endpoint devolve 200 sempre.** Devolver erro faria o Grafana reenviar a notificação, e um
  webhook que falha sozinho viraria tráfego de 5xx — alimentando o alerta de taxa de erro que
  acabou de disparar. Alerta que causa alerta é um laço difícil de enxergar depois.

O `values` do payload traz o valor de cada `refId` da regra. Como `B` é o `reduce`, o log diz
**com quanto** disparou, não só que disparou:

```text
Alerta High Latency DISPAROU (warning) com valor 2.4247378788238074: P95 de latencia acima de 1s
```

### 2. O ciclo de vida inteiro, lido do Loki

Esta é a prova de que o alerta virou sinal como qualquer outro:

| Horário | Estado | Alerta | Valor | `level` |
|---|---|---|---|---|
| 20:20:43 | DISPAROU | High Error Rate | `1` (100 %) | `warning` |
| 20:27:43 | RESOLVIDO | High Error Rate | — | `info` |
| 20:32:16 | DISPAROU | High Latency | `2.42` s | `warning` |
| 20:36:40 | DISPAROU | Application Unhealthy | `0` | `warning` |
| 20:38:40 | RESOLVIDO | Application Unhealthy | — | `info` |
| 20:39:16 | RESOLVIDO | High Latency | — | `info` |

Cada uma dessas linhas tem `TraceId` preenchido — o POST do Grafana é uma requisição HTTP como
outra qualquer e a fase 06 continua valendo para ela.

### 3. Os cinco testes

**Teste 1 — High Error Rate.** `-Cenario erro -Duracao 200`, com o estado lido a cada 15 s:

```text
20:19:28  inactive
20:19:44  pending     ← limiar cruzado, `for` começou a contar
20:20:45  firing      ← 1 min depois, como o `for: 1m` manda
```

A transição por `Pending` durou exatamente as **duas avaliações** que o `interval: 30s` do grupo
permite dentro de 1 minuto. É a diferença entre "picozinho" e "problema", visível no relógio.

**Teste 2 — High Latency.** `-Cenario lento -Delay 2000 -Duracao 240`: `pending` às 20:30:07,
`firing` às 20:32:09 — os 2 minutos do `for`, também no relógio.

> ⚠️ **Uma ressalva honesta ao enunciado do teste.** O plano diz para reparar que "a média
> continua aceitável enquanto o P95 estoura". Com o cenário `lento` **puro** isso não acontece:
> se toda requisição leva 2 s, média e P95 coincidem (medido: média 1.997 ms, P95 2.425 ms).
> A divergência precisa de tráfego **misto** — e ela foi medida na [fase 07](07-dashboard-grafana.md#resultado-da-execucao):
> média 276 ms contra P95 2.346 ms rodando `misto` e `lento` ao mesmo tempo. O alerta de latência
> pega os dois casos; quem esconde a cauda é a média, não o P95.

**Teste 3 — Application Unhealthy.** `docker stop sqlserver_container` às 20:34:40 → `pending` às
20:35:41 → `firing` às 20:36:42. Cerca de 2 min, e não 1: entre a queda e o alerta existem quatro
esperas empilhadas — o publisher de health (15 s), o export OTLP (15 s), o scrape do Prometheus
(15 s) e só então o `for: 1m`. **Latência de detecção é a soma da cadeia inteira**, nunca só o
`for`. Vale saber disso antes de prometer um SLA de detecção.

**Teste 4 — resolução sozinha.** Nenhum alerta precisou de intervenção:

| Alerta | Condição terminou | Voltou a Normal | Atraso |
|---|---|---|---|
| High Error Rate | 20:22:35 (carga parou) | 20:27:47 | ~5 min — a janela do `rate([5m])` esvaziando |
| Application Unhealthy | 20:37:49 (`docker start`) | 20:38:36 | ~47 s |
| High Latency | 20:33:40 (carga parou) | 20:38:51 | ~5 min — mesma janela |

> 💡 **O `for` só vale na ida.** A volta é imediata: assim que a condição deixa de ser verdadeira,
> o Grafana resolve sem esperar nada. Quem segurou os ~5 min dos dois primeiros não foi o alerta,
> foi o `[5m]` da query — a métrica ainda enxergava o incidente. Janela de query é meia-vida de
> alerta, e é por isso que `rate([1h])` em regra de alerta é uma armadilha.

**Teste 5 — persistência.** `docker compose --profile obs down` + `up -d`: as três regras, o
contact point `lab-local` e a policy voltaram provisionados, sem nenhum clique.

### 4. Detalhes de provisioning que só aparecem fazendo

> ⚠️ **A pasta `provisioning/alerting/` precisa existir antes do Grafana subir.** Se o diretório
> não existe, o log solta `can't read alerting provisioning files from directory` e segue em
> frente — sem regra nenhuma e sem falhar. Como o volume é `provisioning/` inteiro, criar a pasta
> depois exige reiniciar o container.

> ⚠️ **O contact point padrão não some.** Depois do provisioning, `/api/v1/provisioning/contact-points`
> lista `email receiver` (o embutido) **e** `lab-local`. Quem decide o destino é a notification
> policy, não a existência do contact point — e a policy provisionada, essa sim, substitui a
> árvore inteira.

> 💡 **`__dashboardUid__` e `__panelId__` não são anotações comuns.** São os dois campos que ligam
> a regra ao painel: com eles, a notificação ganha o botão que abre exatamente o gráfico da faixa 1
> que disparou. É o "próximo passo" saindo do texto e virando clique.
