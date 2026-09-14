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

- [x] Todo log de requisição traz `TraceId`, `SpanId` e `CorrelationId` preenchidos
- [x] `X-Correlation-Id` enviado pelo cliente é honrado e devolvido na resposta — e id inválido cai no `TraceId`
- [x] Sem header, o `TraceId` é adotado como correlation id
- [x] Do log, o botão **Ver trace** abre o trace correto — conferido pela API do Loki/Tempo, com o `matcherRegex` e o `$$` validados no lado do Grafana
- [x] Do span, **Logs for this span** traz os logs daquela requisição — conferido executando a `query` do `tracesToLogsV2`
- [x] O fluxo métrica → trace → log → causa foi percorrido de ponta a ponta

---

<a id="resultado-da-execucao"></a>

## Resultado da execução (2026-09-12)

### O que entrou

| Arquivo | O que mudou |
|---|---|
| `src/CoreFinance.API/Middlewares/CorrelationIdMiddleware.cs` | novo — define o id, marca o span, devolve o header, empurra para o `LogContext` |
| `src/CoreFinance.API/Program.cs` | `UseMiddleware<CorrelationIdMiddleware>()` como **primeiro** middleware da aplicação |
| `docker/grafana/provisioning/datasources/datasources.yml` | `derivedFields` no Loki (log → trace) e `tracesToLogsV2` no Tempo (trace → log) |

Nenhum pacote novo, nenhum container novo — exatamente como o plano previa.

### Desvio do plano: o id de fora é entrada não confiável

O plano usa o header do cliente direto:

```csharp
var correlationId = context.Request.Headers[HeaderName].FirstOrDefault() ?? ...
```

Esse valor vai parar em **três** lugares que não são texto livre: uma tag de span, uma propriedade
de log estruturado e um header de resposta. Um `X-Correlation-Id` com quebra de linha, aspas ou 4 KB
de lixo é log poluído no melhor caso e linha de log forjada no Loki no pior. A execução acrescentou
uma validação mínima — até 64 caracteres, só `[A-Za-z0-9-_.:]` — e quem manda algo fora disso
simplesmente cai no `TraceId`, sem erro e sem requisição rejeitada. `teste-manual-001` passa; o lixo
não. É a mesma decisão que já tinha sido tomada no log: nunca confiar no que veio de fora só porque
é conveniente.

### A ordem confirmada na prática

Com o `CorrelationIdMiddleware` por fora de tudo, os **dois** logs de um 500 saem correlacionados —
o do `UseSerilogRequestLogging` e o do `GlobalExceptionMiddleware`:

```json
{"@mt":"HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms",
 "@l":"Error","@tr":"6808c059b09097eb5dcf83997e562e33","@sp":"73d0aea4fe66e715",
 "RequestPath":"/api/demo/error","StatusCode":500,
 "CorrelationId":"teste-manual-001","SpanId":"73d0aea4fe66e715",
 "TraceId":"6808c059b09097eb5dcf83997e562e33"}

{"@mt":"Erro não tratado: {Message}","@l":"Error",
 "SourceContext":"CoreFinance.API.Middlewares.GlobalExceptionMiddleware",
 "CorrelationId":"teste-manual-001","SpanId":"73d0aea4fe66e715",
 "TraceId":"6808c059b09097eb5dcf83997e562e33"}
```

`ParentId` vem `0000000000000000` em todos eles: o trace nasce na API porque o `web` ainda não
repassa `traceparent` (previsto na [fase 10](10-extras-e-proximos-passos.md)).

### Validado sem a stack

O Docker Desktop estava desligado na execução, então a API subiu direto (`dotnet run`) com o console
usando o mesmo `CompactJsonFormatter` que o sink do Loki usa — o que torna a linha do console
idêntica, em forma, à que chega ao Loki:

| Cenário | Resultado |
|---|---|
| `curl -H "X-Correlation-Id: teste-manual-001" /api/demo/error` | header volta com o mesmo valor; os dois logs do erro trazem `CorrelationId=teste-manual-001` |
| `curl /api/demo/success` (sem header) | `CorrelationId` = `TraceId` = `bfc48794f585c2452689019394d64966` — o `TraceId` foi adotado |
| `curl -H "X-Correlation-Id: lixo com espaço e \" aspas"` | id rejeitado, `TraceId` adotado, requisição respondida normalmente |
| regex do `derivedField` contra as linhas reais | `"TraceId":"[a-f0-9]{32}"` casou em **todas** as linhas de requisição |

O último item é o que costuma quebrar em silêncio, e é o que dá para conferir sem Grafana: se o
regex casa no JSON que a aplicação produz hoje, o botão **Ver trace** tem do que se alimentar.

### Validado com a stack no ar (2026-09-12)

`docker compose --profile obs up -d` **não** basta: o compose reaproveita a imagem já construída e
a API subiu sem o middleware novo — o `X-Correlation-Id` simplesmente não voltava. `--build api`
resolve. Vale para toda fase que mexe em código C#.

Os quatro fluxos foram percorridos pela API do Grafana/Loki/Tempo/Prometheus, que é o mesmo caminho
que os botões da UI usam por baixo:

| Fluxo | Como foi conferido | Resultado |
|---|---|---|
| **A** log → trace | `{app="corefinance-api"} \| json \| CorrelationId="teste-manual-001"` | 3 logs, todos com `TraceId=33bd3c1a…`; o `matcherRegex` do derivedField casou nas 3 linhas cruas |
| **A** (destino) | `GET /api/traces/33bd3c1a…` no Tempo | span `GET api/Demo/error`, `STATUS_CODE_ERROR`, tag `correlation.id = teste-manual-001` e evento `exception` |
| **B** trace → log | a `query` do `tracesToLogsV2`, com o traceId no lugar da variável | exatamente os 3 logs daquela requisição, e só dela |
| **C** métrica → causa | `increase(http_server_request_duration_seconds_count{...500}[5m])` → `{ status = error }` → span → logs do trace | 5,3 req/5m em `api/Demo/error` → trace → `System.InvalidOperationException: Erro proposital…` |
| **D** sem header | `RequestPath="/api/demo/success"` | `CorrelationId == TraceId == 12b62415…`, o mesmo valor que voltou no header |

### O `$$` sobreviveu — conferido no lado do Grafana

A armadilha dos dois cifrões só se revela depois que o Grafana lê o arquivo. O que ele guardou:

```json
"url": "${__value.raw}"
"query": "{app=\"corefinance-api\"} | json | TraceId=\"${__span.traceId}\""
```

Um cifrão de cada, que é a forma que o runtime interpola. Se o provisioning tivesse `$` simples, o
que apareceria aqui seria a variável **já resolvida como vazia** — e o botão existiria, clicável,
levando a lugar nenhum.

### A busca vazia de trinta segundos

Na primeira tentativa, segundos depois da requisição, **tanto** o filtro por `TraceId` no Loki
quanto o `{ span.correlation.id = "…" }` no Tempo voltaram vazios — enquanto buscar o trace pelo id
direto (`/api/traces/<id>`) já funcionava. Minutos depois, as mesmas consultas devolveram tudo.

É a mesma defasagem que motiva o `spanStartTimeShift`/`spanEndTimeShift`, vista de outro ângulo:
buscar por atributo depende de dado já indexado, ler por id não. Quem valida a correlação no
instante seguinte ao request conclui que ela não funciona — e mexe no que estava certo. Esperar um
pouco e alargar a janela de tempo é parte do teste, não impaciência.
