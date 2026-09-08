using CoreFinance.API.Health;
using CoreFinance.Application.Common.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace CoreFinance.API.Observability;

public static class ObservabilityExtensions
{
    private const string ServiceNamePadrao = "corefinance-api";

    /// <summary>
    /// Registra as métricas da aplicação e o pipeline do OpenTelemetry (API → OTLP → Collector).
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // AppMetrics e singleton e tem duas faces: IAppMetrics para a Application (que nao pode
        // conhecer OpenTelemetry) e o tipo concreto para o publisher de health, que e da API.
        // A segunda linha resolve a mesma instancia — sem ela seriam dois Meters iguais.
        services.AddSingleton<AppMetrics>();
        services.AddSingleton<IAppMetrics>(sp => sp.GetRequiredService<AppMetrics>());

        // O runtime executa os health checks em background e entrega o relatorio ao publisher.
        // Delay = espera depois da partida; Period = de quanto em quanto tempo repete.
        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(5);
            options.Period = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<IHealthCheckPublisher, HealthMetricsPublisher>();

        // Desligar a exportacao nao desliga a instrumentacao do codigo: o Meter continua la,
        // so nao ha ninguem coletando. E a saida para rodar sem o profile "obs" no ar.
        if (!configuration.GetValue("Observability:Enabled", true))
        {
            return services;
        }

        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services.AddOpenTelemetry()
            // O Resource e o que carimba service.name em TODO sinal exportado. Sem ele os dados
            // chegam anonimos no backend e nao da para separar aplicacoes.
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: configuration["OTEL_SERVICE_NAME"] ?? ServiceNamePadrao,
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString())
                .AddAttributes(AtributosPadrao(configuration, environment)))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()   // http.server.request.duration
                    .AddHttpClientInstrumentation()   // chamadas HTTP de saida
                    .AddRuntimeInstrumentation()      // GC, heap, threads
                    .AddMeter(AppMetrics.MeterName);  // as nossas

                metrics.AddOtlpExporter(options =>
                {
                    // Protocolo e headers continuam vindo das variaveis OTEL_* padrao; aqui so
                    // fixamos o destino. Se um dia trocar para http/protobuf, lembrar que o
                    // caminho /v1/metrics nao e anexado quando o Endpoint vem por codigo.
                    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    }
                });
            });

        return services;
    }

    /// <summary>
    /// Identidade dos sinais, com <c>OTEL_RESOURCE_ATTRIBUTES</c> tendo a última palavra.
    /// </summary>
    // O compose define deployment.environment=local — mesmo valor do label env do Loki. Se o
    // codigo carimbasse EnvironmentName por cima, a metrica diria "Development" e o log diria
    // "local" para a mesma execucao, e a correlacao da fase 06 nasceria torta. Entao aqui so
    // entram as chaves que o ambiente NAO definiu: rodando fora do compose (Visual Studio),
    // onde a variavel nao existe, valem estes padroes.
    private static Dictionary<string, object> AtributosPadrao(
        IConfiguration configuration, IHostEnvironment environment)
    {
        var definidosNoAmbiente = (configuration["OTEL_RESOURCE_ATTRIBUTES"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(par => par.Split('=', 2)[0].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var atributos = new Dictionary<string, object>
        {
            ["deployment.environment"] = environment.EnvironmentName,
            ["service.namespace"] = "corefinance"
        };

        return atributos
            .Where(a => !definidosNoAmbiente.Contains(a.Key))
            .ToDictionary(a => a.Key, a => a.Value);
    }
}
