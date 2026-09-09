using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace CoreFinance.API.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            MarcarSpanComoErro(ex);
            _logger.LogError(ex, "Erro não tratado: {Message}", ex.Message);
            await EscreverRespostaDeErroAsync(context, ex);
        }
    }

    /// <summary>
    /// Marca a <see cref="Activity"/> da requisição como falha e anexa a exceção ao span.
    /// </summary>
    // Este middleware ENGOLE a excecao para devolver o JSON de erro — o que significa que a
    // instrumentacao do ASP.NET Core nunca a ve, e o RecordException dela nao dispara. Sem estas
    // duas linhas o trace de um 500 chega ao Tempo com aparencia de trace normal e some no meio
    // dos outros; com elas ele nasce vermelho e e achavel por `{ status = error }`.
    //
    // Activity.Current e o span vivo da requisicao (criado pela instrumentacao do ASP.NET Core).
    // E null quando a exportacao esta desligada ou a rota foi filtrada — por isso a saida cedo.
    private static void MarcarSpanComoErro(Exception ex)
    {
        var span = Activity.Current;

        if (span is null)
        {
            return;
        }

        span.SetStatus(ActivityStatusCode.Error, ex.Message);

        // AddException e da BCL (System.Diagnostics), nao do OpenTelemetry — o pacote traz a API
        // do .NET 9 para o net8.0 via System.Diagnostics.DiagnosticSource. O RecordException do
        // OpenTelemetry.Trace faz o mesmo, mas esta marcado como obsoleto e some numa proxima
        // versao. Anexa a excecao como EVENTO do span: tipo, mensagem e stack trace.
        span.AddException(ex);
    }

    private static async Task EscreverRespostaDeErroAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var resposta = new
        {
            status = context.Response.StatusCode,
            erro = "Ocorreu um erro interno no servidor.",
            detalhe = ex.Message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(resposta));
    }
}
