using System.ComponentModel.DataAnnotations;
using CoreFinance.API.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreFinance.API.Controllers;

// Controller de laboratorio: provoca sucesso, erro e lentidao sob demanda, para as fases
// seguintes da trilha de observabilidade terem sinal para observar.
//
// Nao herda de BaseController de proposito: BaseController traz [Authorize] e os helpers de
// Result, e nada disso se aplica aqui — isto nao e recurso de negocio, e instrumento de teste.
// Tambem nao tem service nem repositorio: o cenario e a propria logica, entao nao ha regra de
// negocio no controller.
//
// So entra no roteamento quando Observability:Demo:Enabled = true (ver DemoControllerConvention).
[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
[Produces("application/json")]
public class DemoController : ControllerBase
{
    private const int DelayMaximoEmMs = 10_000;

    private readonly ILogger<DemoController> _logger;
    private readonly IHostEnvironment _ambiente;

    public DemoController(ILogger<DemoController> logger, IHostEnvironment ambiente)
    {
        _logger = logger;
        _ambiente = ambiente;
    }

    [HttpGet("success")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Sucesso()
    {
        _logger.LogInformation("Demo success executado em {Ambiente}", _ambiente.EnvironmentName);

        return Ok(new
        {
            cenario = "success",
            ambiente = _ambiente.EnvironmentName,
            em = DateTimeOffset.UtcNow
        });
    }

    // Lanca de verdade, em vez de devolver StatusCode(500): o objetivo e atravessar o
    // GlobalExceptionMiddleware real e ver a excecao virar log com stack trace — e, na fase 05,
    // span com status de erro.
    [HttpGet("error")]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult Erro()
    {
        _logger.LogWarning("Demo error vai lancar excecao proposital em {Ambiente}", _ambiente.EnvironmentName);

        throw new InvalidOperationException("Erro proposital do endpoint de demonstracao.");
    }

    [HttpGet("slow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Lento(
        [FromQuery][Range(0, DelayMaximoEmMs)] int delay = 3000,
        CancellationToken cancellationToken = default)
    {
        // Task.Delay, nunca Thread.Sleep: prender uma thread do pool distorceria justamente as
        // metricas de runtime que a fase 04 vai medir.
        await Task.Delay(delay, cancellationToken);

        _logger.LogInformation("Demo slow respondeu apos {DelayMs}ms", delay);

        return Ok(new { cenario = "slow", delayMs = delay });
    }

    [HttpGet("random")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Aleatorio(
        [FromQuery][Range(0, 100)] int errorRate = 5,
        CancellationToken cancellationToken = default)
    {
        if (System.Random.Shared.Next(100) < errorRate)
        {
            _logger.LogWarning("Demo random sorteou erro com taxa {ErrorRate}%", errorRate);

            throw new InvalidOperationException("Erro sorteado pelo endpoint de demonstracao.");
        }

        var (rotulo, delayMs) = SortearLatencia();
        await Task.Delay(delayMs, cancellationToken);

        _logger.LogInformation(
            "Demo random respondeu {Latencia} em {DelayMs}ms com taxa {ErrorRate}%",
            rotulo, delayMs, errorRate);

        return Ok(new { cenario = "random", latencia = rotulo, delayMs, errorRate });
    }

    // Destino do contact point da fase 08. Nao existe para "receber notificacao": existe para
    // o alerta virar LOG — e, no Loki, ele fica pesquisavel como qualquer outro sinal, com
    // TraceId e tudo. E a observabilidade observando a si mesma, que e o fecho da trilha.
    [HttpPost("alert-webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult AlertaDoGrafana([FromBody] AlertaWebhookRequest requisicao)
    {
        foreach (var alerta in requisicao.Alerts)
        {
            var nome = alerta.Labels.GetValueOrDefault("alertname", "desconhecido");
            var severidade = alerta.Labels.GetValueOrDefault("severity", "desconhecida");
            var resumo = alerta.Annotations.GetValueOrDefault("summary", string.Empty);

            // B e o refId do reduce em rules.yml — o numero que cruzou o limiar.
            var valor = alerta.Values.GetValueOrDefault("B");

            // Disparo em Warning, resolucao em Information: o nivel e a unica propriedade
            // promovida a label do Loki (fase 03), entao `{app="corefinance-api", level="warning"}`
            // passa a listar os alertas que dispararam sem precisar ler o texto da linha.
            if (string.Equals(alerta.Status, "firing", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Alerta {Alerta} DISPAROU ({Severidade}) com valor {Valor}: {Resumo}",
                    nome, severidade, valor, resumo);
            }
            else
            {
                _logger.LogInformation(
                    "Alerta {Alerta} RESOLVIDO ({Severidade}): {Resumo}",
                    nome, severidade, resumo);
            }
        }

        // 200 sempre: devolver erro faria o Grafana reenviar a notificacao, e um webhook que
        // falha sozinho viraria trafego de erro — alimentando o proprio alerta de taxa de erro.
        return Ok(new { cenario = "alert-webhook", recebidos = requisicao.Alerts.Count });
    }

    // Cauda de latencia proposital: a maioria rapida, algumas medianas, poucas lentas.
    // E o que faz media e P95 divergirem — a licao da fase 04. Uma distribuicao chapada
    // produziria grafico bonito e nenhum aprendizado.
    private static (string Rotulo, int DelayMs) SortearLatencia()
        => System.Random.Shared.Next(100) switch
        {
            < 80 => ("rapida", System.Random.Shared.Next(10, 60)),
            < 95 => ("mediana", System.Random.Shared.Next(100, 400)),
            _ => ("lenta", System.Random.Shared.Next(800, 2500))
        };
}
