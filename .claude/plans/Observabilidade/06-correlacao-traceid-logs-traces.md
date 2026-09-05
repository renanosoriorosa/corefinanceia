# Fase 06 — Correlação: Trace ID ligando logs e traces

> ⬅️ anterior: [05 — Traces](05-traces-tempo.md) · ➡️ próxima: [07 — Dashboard](07-dashboard-grafana.md)
> **Containers novos:** nenhum. Esta fase não adiciona ferramenta — **conecta** as que já existem. É o coração do laboratório (seção 27 da spec).

---

## Objetivo pedagógico

Responder à pergunta **9**: *como encontrar todos os logs relacionados a uma requisição específica?*

Até aqui você tem três silos: métricas dizem que há erro, traces mostram a requisição, logs contam o que houve. **Correlação é o que transforma três ferramentas em um sistema.** Sem ela, achar a causa de um erro específico significa filtrar log por horário e torcer.

O fluxo-alvo:

```text
Erro no dashboard
      ↓
Métrica de erro          (fase 04)
      ↓
Trace da requisição      (fase 05)
      ↓
Trace ID
      ↓
Logs daquela requisição  (fase 03)
      ↓
Causa identificada
```

E o caminho inverso: de um log de erro, um clique abre o trace.

---

## O que entra no projeto

**Arquivos novos:**

```text
src/CoreFinance.API/Middlewares/CorrelationIdMiddleware.cs
```

**Arquivos alterados:**

```text
src/CoreFinance.API/Program.cs                            ← ordem do middleware
docker/grafana/provisioning/datasources/datasources.yml   ← derivedFields + tracesToLogsV2
```

Nenhum pacote novo — `Serilog.Enrichers.Span` já entrou na [fase 03](03-logs-serilog-loki.md) justamente pensando neste momento.

---

## Passos

### 1. Verificar que o `TraceId` já está nos logs

Depois da fase 05, o `Enrich.WithSpan()` passou a encontrar uma `Activity.Current` de verdade. Em `docker logs corefinance-api` (ou no Loki com `| json`) cada linha deve trazer:

```json
{ "@t":"...", "@m":"HTTP GET /api/demo/error respondeu 500 em 12,3 ms",
  "TraceId":"4bf92f3577b34da6a3ce929d0e0e4736", "SpanId":"00f067aa0ba902b7", "app":"corefinance-api" }
```

Se `TraceId` estiver vazio, o problema é ordem: o log foi emitido fora do escopo de uma `Activity` (antes do middleware de tracing ou em background).

### 2. `CorrelationIdMiddleware`

O `TraceId` resolve correlação **dentro** do sistema. O *correlation id* resolve correlação com o **mundo de fora**: o suporte recebe um print com um id e precisa achar a requisição.

```csharp
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // usa o que o cliente mandou, ou adota o TraceId do OpenTelemetry
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Activity.Current?.TraceId.ToString()
                            ?? Guid.NewGuid().ToString("N");

        Activity.Current?.SetTag("correlation.id", correlationId);
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

Três coisas ao mesmo tempo: entra na `Activity` (achável no Tempo por `{ span.correlation.id = "..." }`), volta no header da resposta (o cliente pode registrar) e entra no `LogContext` (aparece em **todo** log daquela requisição).

> A spec (seção 9) diz: use o contexto do OpenTelemetry quando possível e só crie middleware se necessário. A escolha aqui é usar o `TraceId` como valor padrão — o middleware só honra um id vindo de fora, sem inventar um identificador paralelo.

### 3. Ordem no `Program.cs` — importa muito

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();   // 1º: define o contexto
app.UseMiddleware<GlobalExceptionMiddleware>(); // 2º: já loga com contexto
app.UseSerilogRequestLogging();
...
```

Se o `GlobalExceptionMiddleware` vier antes, o log do erro — justamente o que você mais quer correlacionar — sai sem `CorrelationId`.

> ⚠️ Detalhe sutil: o `CorrelationIdMiddleware` precisa rodar **depois** que o OpenTelemetry criou a `Activity` da requisição. A instrumentação do ASP.NET Core cria a `Activity` no início do pipeline (via `DiagnosticSource`), então qualquer middleware da aplicação já a encontra em `Activity.Current`. Se `Activity.Current` vier `null`, o fallback para `Guid` cobre.

### 4. Loki → Tempo (o botão "ver trace" dentro do log)

Em `datasources.yml`, no datasource Loki:

```yaml
  - name: Loki
    type: loki
    uid: loki
    access: proxy
    url: http://loki:3100
    jsonData:
      derivedFields:
        - name: TraceID
          matcherType: regex
          matcherRegex: '"TraceId":"([a-f0-9]{32})"'
          url: '$${__value.raw}'
          datasourceUid: tempo
          urlDisplayLabel: 'Ver trace'
```

O Grafana aplica o regex em cada linha e, quando casa, renderiza um **botão** no detalhe do log que abre o trace no Tempo.

> ⚠️ **`$${__value.raw}` com dois cifrões.** No provisioning YAML, `$` é interpolado pelo Grafana/compose; o escape duplo é obrigatório. Um cifrão só produz um link quebrado e nenhuma mensagem de erro — armadilha clássica.

> 💡 O regex casa com o JSON produzido pelo `CompactJsonFormatter` da fase 03. Se você mudar o formatter, **atualize o regex**. Valide primeiro no Explore: `{app="corefinance-api"} | json | line_format "{{.TraceId}}"`.

### 5. Tempo → Loki (o caminho de volta)

No datasource Tempo:

```yaml
    jsonData:
      tracesToLogsV2:
        datasourceUid: loki
        spanStartTimeShift: '-5m'
        spanEndTimeShift: '5m'
        filterByTraceID: true
        tags:
          - key: 'service.name'
            value: 'app'
        customQuery: true
        query: '{app="corefinance-api"} | json | TraceId="$${__span.traceId}"'
      nodeGraph:
        enabled: true
```

Agora, dentro de um trace, cada span tem um botão **Logs for this span**.

Os `spanStartTimeShift`/`EndTimeShift` existem por causa de **defasagem de relógio e de batch**: o log pode chegar ao Loki alguns segundos depois do span chegar ao Tempo. Sem a folga, a busca volta vazia e parece que a correlação não funciona.

---

## Como validar — o teste que define o laboratório

```powershell
# 1. Provocar um erro com um id conhecido
curl -i -H "X-Correlation-Id: teste-manual-001" http://localhost:5176/api/demo/error
# o header X-Correlation-Id volta na resposta
```

**Fluxo A — do log para o trace:**

1. Grafana → Explore → Loki → `{app="corefinance-api"} | json | CorrelationId="teste-manual-001"`
2. Expandir a linha de log → clicar em **Ver trace**
3. O trace abre no Tempo, com os spans e a exceção

**Fluxo B — do trace para o log:**

1. Explore → Tempo → `{ status = error }` → abrir um trace
2. Copiar o Trace ID
3. Em um span, clicar em **Logs for this span**
4. Os logs daquela requisição — e só daquela — aparecem

**Fluxo C — o completo, da métrica à causa:**

1. Prometheus/Grafana: a taxa de erro subiu
2. Tempo: `{ status = error }` no mesmo intervalo
3. Abrir o trace, ver qual span falhou
4. Do span, ir para os logs
5. Ler a exceção e a mensagem — causa identificada

**Fluxo D — sem id externo:** repetir sem o header `X-Correlation-Id` e confirmar que o `TraceId` foi adotado como correlation id (os dois valores coincidem no log).

---

## Dicas e armadilhas

> 💡 **Trace ID vs Correlation ID.** O Trace ID é gerado pela instrumentação e é técnico. O Correlation ID é de negócio/suporte: pode vir do gateway, do front, de um id de pedido. Aqui os dois coincidem por padrão, mas eles têm donos diferentes — e é bom entender por que muitas empresas mantêm ambos.

> 💡 **Propagação entre serviços.** Se o CoreFinance chamasse outra API, o `HttpClient` instrumentado enviaria o header `traceparent` sozinho e o trace continuaria do outro lado. É por isso que se chama *distributed* tracing e é a razão de o padrão W3C existir. Vale testar de brincadeira: mande `traceparent` na mão e veja seu span virar filho de um trace inventado.

> ⚠️ **O `web` (Next.js) ainda corta a corrente.** O proxy em `web/src/app/api/[...path]/route.ts` repassa o `authorization`, mas não o `traceparent`. Então um trace começa na API, não no navegador. Repassar esse header é uma melhoria pequena e muito ilustrativa — está em [10 — Extras](10-extras-e-proximos-passos.md).

> ⚠️ **Correlação depende de relógio.** Containers no mesmo host compartilham o relógio, então aqui não dói. Em máquinas diferentes, sem NTP, a correlação por janela de tempo simplesmente falha. É uma das causas mais frustrantes de "não acho os logs do trace".

> 💡 **Logs de background não têm trace.** `HostedService`, tarefas agendadas e jobs rodam fora de uma requisição, então `TraceId` fica vazio. A solução é criar uma `Activity` própria para cada execução — vale lembrar quando a Fase 4 do roadmap adicionar as notificações de vencimento.

---

## Conceitos aprendidos

- **Correlação** como a propriedade que une os três sinais.
- **W3C Trace Context** (`traceparent`) e propagação entre serviços.
- `LogContext.PushProperty` e escopo de log.
- **Derived fields** (Loki→Tempo) e **tracesToLogs** (Tempo→Loki).
- Por que folga de tempo é necessária ao correlacionar sistemas diferentes.
- Ordem de middleware como decisão de observabilidade, não só de funcionalidade.

---

## Critério de aceite

- [ ] Todo log de requisição traz `TraceId`, `SpanId` e `CorrelationId` preenchidos
- [ ] `X-Correlation-Id` enviado pelo cliente é honrado e devolvido na resposta
- [ ] Sem header, o `TraceId` é adotado como correlation id
- [ ] Do log, o botão **Ver trace** abre o trace correto
- [ ] Do span, **Logs for this span** traz os logs daquela requisição
- [ ] O fluxo métrica → trace → log → causa foi percorrido de ponta a ponta
