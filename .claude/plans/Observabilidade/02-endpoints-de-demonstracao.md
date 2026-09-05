# Fase 02 — Endpoints de demonstração e geração de carga

> ⬅️ anterior: [01 — Health Checks](01-health-checks.md) · ➡️ próxima: [03 — Logs](03-logs-serilog-loki.md)
> **Containers novos:** nenhum. Fase curta, mas **precisa vir cedo**: todas as fases seguintes dependem dela para ter sinal para observar.
> **Status:** ✅ concluída e validada em 2026-09-05.

---

## Objetivo pedagógico

Observabilidade sem tráfego é uma tela vazia. Esta fase cria a **matéria-prima**: um jeito de provocar sucesso, erro, lentidão e comportamento aleatório **sem editar código toda vez** (exigência da seção 20 da spec).

O conceito por trás: **testabilidade da observabilidade**. Você não confia em um alerta que nunca viu disparar. Precisa de um botão para quebrar de propósito.

---

## O que entra no projeto

**Arquivos novos:**

```text
src/CoreFinance.API/Controllers/DemoController.cs
scripts/gerar-carga.ps1
```

**Arquivos alterados:**

```text
src/CoreFinance.API/appsettings.Development.json   ← Observability:Demo:Enabled = true
docker-compose.yml                                  ← Observability__Demo__Enabled: "true"
```

Nenhum arquivo de `Application`, `Domain` ou `Infra` é tocado. **Isso é proposital** e vale dizer em voz alta: instrumentação e cenário de teste não são domínio.

---

## Passos

### 1. `Controllers/DemoController.cs`

```csharp
[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
[Produces("application/json")]
public class DemoController : ControllerBase   // NÃO herda de BaseController
{
    private readonly ILogger<DemoController> _logger;

    public DemoController(ILogger<DemoController> logger) => _logger = logger;
    ...
}
```

Três decisões dentro dessas cinco linhas:

- **Não herda de `BaseController`** — `BaseController` tem `[Authorize]` e os helpers de `Result`. Nada disso se aplica: não é recurso de negócio, é instrumento de laboratório.
- **`[AllowAnonymous]`** — gerar carga com token expirando no meio atrapalha o estudo.
- **Sem service, sem repositório** — a lógica é o próprio cenário. Não existe regra de negócio aqui para violar a regra do `CLAUDE.md`.

### 2. Os quatro endpoints

| Endpoint | Comportamento | Serve para |
|---|---|---|
| `GET /api/demo/success` | 200 + `LogInformation` estruturado | linha de base de métrica e log |
| `GET /api/demo/error` | lança exceção controlada → 500 pelo `GlobalExceptionMiddleware` já existente | taxa de erro, log de erro, trace vermelho |
| `GET /api/demo/slow?delay=3000` | `Task.Delay(delay)` (limitar a 10 s) | latência, P95, alerta de lentidão |
| `GET /api/demo/random?errorRate=50` | sorteia rápido / lento / erro conforme o percentual | gráficos com forma, alertas realistas |

Detalhes que valem cuidado:

- `slow` usa `await Task.Delay(delay, cancellationToken)` — **nunca** `Thread.Sleep`. `Thread.Sleep` prende uma thread do pool e distorce justamente as métricas de runtime que a [fase 04](04-metricas-otel-collector-prometheus.md) vai medir.
- `error` **lança**, não retorna `StatusCode(500)`. O objetivo é passar pelo `GlobalExceptionMiddleware` real e ver a exceção virar log com stack trace e, mais adiante, span com status de erro.
- `random` usa `Random.Shared`.
- Validar os parâmetros na entrada (`delay` 0–10000, `errorRate` 0–100) — evita `delay=999999999` derrubando o lab.

### 3. Flag de configuração

O controller só deve existir quando ligado. Em `Program.cs`:

```csharp
if (!builder.Configuration.GetValue<bool>("Observability:Demo:Enabled"))
{
    builder.Services.AddControllers(options => { /* remover/ignorar DemoController */ });
}
```

Forma mais limpa e idiomática — um `IActionModelConvention` ou um `ApplicationModelProvider` que remove o controller quando a flag está desligada. Alternativa mais simples e igualmente válida para um lab: um `ActionFilter`/middleware que devolve 404 nas rotas `/api/demo/*` com a flag desligada.

O ponto pedagógico: **funcionalidade de laboratório precisa de interruptor**, e o interruptor mora na configuração, não no `#if DEBUG`.

### 4. `scripts/gerar-carga.ps1`

Um laço PowerShell com parâmetros:

```powershell
.\scripts\gerar-carga.ps1                                  # tráfego misto por 60s
.\scripts\gerar-carga.ps1 -Cenario erro    -Duracao 120    # 100% erro, para o alerta
.\scripts\gerar-carga.ps1 -Cenario lento   -Delay 3000     # latência alta
.\scripts\gerar-carga.ps1 -Cenario volume  -Paralelo 20    # muitas requisições
```

Sem container de carga por enquanto: mais uma peça no compose para pouco ganho. O k6 fica como opcional em [10 — Extras](10-extras-e-proximos-passos.md).

---

## Como validar

```powershell
docker compose up -d --build api

curl http://localhost:5176/api/demo/success                    # 200
curl -i http://localhost:5176/api/demo/error                   # 500 + JSON do middleware global
Measure-Command { curl http://localhost:5176/api/demo/slow?delay=3000 }   # ~3s
1..10 | ForEach-Object { curl http://localhost:5176/api/demo/random?errorRate=50 }

docker logs corefinance-api --tail 30   # o erro aparece com stack trace

# flag desligada → rotas somem
# (Observability__Demo__Enabled: "false" no compose, recriar o container)
curl -i http://localhost:5176/api/demo/success   # 404
```

---

## Dicas e armadilhas

> ⚠️ **Deixar a flag ligada em produção é um buraco.** `/api/demo/slow?delay=10000` sem autenticação é um vetor de exaustão de recursos. Neste lab tudo é local, mas a flag existe justamente para você nunca esquecer que ela deveria estar desligada lá fora.

> 💡 **Log estruturado desde já**, mesmo antes do Serilog entrar em cena:
> ```csharp
> _logger.LogInformation("Demo success executado em {Ambiente}", _env.EnvironmentName);  // ✅
> _logger.LogInformation($"Demo success em {ambiente}");                                  // ❌
> ```
> A primeira forma preserva `Ambiente` como **campo pesquisável** no Loki. A segunda produz uma string morta e ainda quebra o agrupamento por template. Pegar o hábito aqui evita retrabalho na [fase 03](03-logs-serilog-loki.md).

> 💡 **Faça o `random` ser realista.** 100% de erro é fácil de alertar e não ensina nada. Uma mistura de ~5% de erro e uma cauda de lentidão produz gráficos parecidos com produção — e é aí que você aprende a diferença entre média e P95.

> 💡 **`slow` também é o teste do timeout.** Depois, em [09](09-testes-e-documentacao.md), ele serve para verificar se o front (`web`) trata resposta lenta e se o proxy do Next.js tem timeout próprio.

---

## Conceitos aprendidos

- **Cenários de falha controlados** como parte do arsenal de observabilidade (parente pobre do chaos engineering).
- **Feature flag por configuração** para código que não pode existir em produção.
- Por que `Task.Delay` e `Thread.Sleep` produzem métricas completamente diferentes.
- Log estruturado por template vs interpolação de string.

---

## Critério de aceite

- [x] Os 4 endpoints respondem conforme a tabela
- [x] `error` passa pelo `GlobalExceptionMiddleware` e aparece nos logs com stack trace (com arquivo e linha)
- [x] Parâmetros fora de faixa são rejeitados (400), não obedecidos — `delay=999999999` e `delay=-1` viram 400 em ~3 ms; os limites válidos (`delay=0`, `delay=10000`, `errorRate=0..100`) passam
- [x] Com a flag desligada, `/api/demo/*` retorna 404 — e some também do `swagger.json`, porque a rota não chega a ser criada
- [x] `scripts/gerar-carga.ps1` roda os 4 cenários
- [x] Nenhum arquivo de `Application`/`Domain`/`Infra` foi alterado (confirmado por `git status` nas três pastas)
