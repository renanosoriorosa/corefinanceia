using CoreFinance.API.Extensions;
using CoreFinance.API.Middlewares;
using CoreFinance.API.Observability;
using CoreFinance.API.Services;
using CoreFinance.Application;
using CoreFinance.Domain.Interfaces;
using CoreFinance.Infra;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var demoHabilitado = builder.Configuration.GetValue<bool>("Observability:Demo:Enabled");

// Toda a configuracao do Serilog (nivel, sinks, URL do Loki) vem de appsettings/ambiente via
// ReadFrom.Configuration — nada hardcoded aqui. Os enrichers sao codigo porque sao contrato do
// laboratorio: todo log sai com maquina e com TraceId/SpanId.
//
// "app" e "env" NAO entram como enricher: eles ja sao labels fixos do sink do Loki. Duplicar
// geraria o pior dos mundos — o label diria env=local e o campo do JSON diria env=Development,
// na mesma linha.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithMachineName());

// O sink do Loki falha em silencio de proposito — observabilidade nao pode derrubar a aplicacao.
// Em Development isso atrapalha o aprendizado: sem o SelfLog, "nao aparece nada no Grafana" fica
// sem diagnostico. Fora de Development continua desligado.
if (builder.Environment.IsDevelopment())
{
    Serilog.Debugging.SelfLog.Enable(Console.Error);
}

builder.Services.AddControllers(options =>
    options.Conventions.Add(new DemoControllerConvention(demoHabilitado)));
builder.Services.AddSwaggerConfig();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfra(builder.Configuration);
builder.Services.AddHealthChecksConfig(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

// Fica DEPOIS do GlobalExceptionMiddleware de proposito: assim a excecao ainda esta viva quando
// passa por aqui, e a requisicao e registrada como Error com stack trace, em vez de virar uma
// linha muda de status 500.
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms";

    options.GetLevel = DefinirNivelDaRequisicao;

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());

        // UserId (Guid), nunca e-mail e nunca o token: o Loki deste lab nao tem controle de acesso.
        var usuario = httpContext.RequestServices.GetService<ICurrentUser>();
        if (usuario is { Autenticado: true })
        {
            diagnosticContext.Set("UserId", usuario.Id);
        }
    };
});

app.UseSwaggerConfig();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecksConfig();
app.MapControllers();

app.Run();

// O HEALTHCHECK do Docker bate em /health/live a cada 30s. Em Information isso viraria a maior
// fonte de log do laboratorio e enterraria o sinal de verdade — entao so sobe de nivel quando falha.
static LogEventLevel DefinirNivelDaRequisicao(HttpContext context, double elapsed, Exception? excecao)
{
    if (excecao is not null || context.Response.StatusCode >= 500)
    {
        return LogEventLevel.Error;
    }

    if (context.Request.Path.StartsWithSegments("/health"))
    {
        return context.Response.StatusCode >= 400 ? LogEventLevel.Warning : LogEventLevel.Debug;
    }

    return context.Response.StatusCode >= 400 ? LogEventLevel.Warning : LogEventLevel.Information;
}
