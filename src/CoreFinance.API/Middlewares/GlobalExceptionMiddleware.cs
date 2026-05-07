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
            _logger.LogError(ex, "Erro não tratado: {Message}", ex.Message);
            await EscreverRespostaDeErroAsync(context, ex);
        }
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
