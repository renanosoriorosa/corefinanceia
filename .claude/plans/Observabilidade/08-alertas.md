# Fase 08 — Alertas no Grafana

> ⬅️ anterior: [07 — Dashboard](07-dashboard-grafana.md) · ➡️ próxima: [09 — Testes e documentação](09-testes-e-documentacao.md)
> **Containers novos:** nenhum.

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

- [ ] Três regras provisionadas e visíveis em Alerting → Alert rules
- [ ] High Error Rate dispara com `-Cenario erro` e a transição por `Pending` foi observada
- [ ] High Latency dispara com `-Cenario lento`
- [ ] Application Unhealthy dispara com o SQL Server parado
- [ ] Todos voltam a `Normal` sozinhos quando a condição termina
- [ ] Limiares centralizados e fáceis de alterar
- [ ] Regras sobrevivem a `down` + `up`
