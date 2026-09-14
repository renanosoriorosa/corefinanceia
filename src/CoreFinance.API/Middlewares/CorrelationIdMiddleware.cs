using System.Diagnostics;
using Serilog.Context;

namespace CoreFinance.API.Middlewares;

/// <summary>
/// Define o identificador de correlação da requisição e o propaga para os três sinais:
/// span (Tempo), log (Loki) e resposta HTTP (cliente).
/// </summary>
// O TraceId ja resolve correlacao DENTRO do sistema. O correlation id resolve correlacao com o
// mundo de fora: o suporte recebe um print com um id e precisa achar a requisicao. Por isso o
// valor vindo do cliente tem prioridade — e, quando nao vem, adotamos o TraceId em vez de
// inventar um identificador paralelo que ninguem mais conhece.
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    // Id de fora e entrada nao confiavel e vai parar em log e em span. O limite de tamanho e o
    // conjunto restrito de caracteres evitam tanto linha de log gigante quanto injecao de quebra
    // de linha / JSON no Loki. Quem mandar lixo simplesmente cai no TraceId.
    private const int TamanhoMaximo = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ObterCorrelationId(context);

        // Achavel no Tempo por `{ span.correlation.id = "..." }`.
        Activity.Current?.SetTag("correlation.id", correlationId);

        // Devolvido ao cliente para que ele registre o id do lado dele. Escrito agora, antes de
        // qualquer byte do corpo sair: depois que a resposta comeca, mexer em header lanca.
        context.Response.Headers[HeaderName] = correlationId;

        // PushProperty e AsyncLocal: vale para TODO log emitido daqui para baixo, inclusive o do
        // GlobalExceptionMiddleware e o do UseSerilogRequestLogging, que sao os que mais importam.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// Honra o id enviado pelo cliente; na ausência dele adota o <c>TraceId</c> do OpenTelemetry.
    /// </summary>
    // Activity.Current e criada pela instrumentacao do ASP.NET Core no inicio do pipeline, entao
    // normalmente ja existe aqui. Fica null quando a exportacao esta desligada
    // (Observability:Enabled = false) — e para esse caso existe o Guid.
    private static string ObterCorrelationId(HttpContext context)
    {
        var doCliente = context.Request.Headers[HeaderName].FirstOrDefault();

        if (EhValido(doCliente))
        {
            return doCliente!;
        }

        var trace = Activity.Current?.TraceId.ToString();

        return string.IsNullOrWhiteSpace(trace)
            ? Guid.NewGuid().ToString("N")
            : trace;
    }

    private static bool EhValido(string? correlationId)
        => !string.IsNullOrWhiteSpace(correlationId)
           && correlationId.Length <= TamanhoMaximo
           && correlationId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
