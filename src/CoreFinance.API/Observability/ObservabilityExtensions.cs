using CoreFinance.API.Extensions;
using CoreFinance.API.Health;
using CoreFinance.Application.Common.Observability;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CoreFinance.API.Observability;

public static class ObservabilityExtensions
{
    private const string ServiceNamePadrao = "corefinance-api";

    /// <summary>
    /// Registra as métricas e os traces da aplicação e o pipeline do OpenTelemetry
    /// (API → OTLP → Collector → Prometheus / Tempo).
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
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // O /health/live e chamado a cada 30s pelo HEALTHCHECK do Docker, para
                        // sempre. Sem este filtro, em pouco tempo a maior parte do storage do
                        // Tempo seria ruido de health check. Mesmo criterio ja aplicado ao log.
                        options.Filter = contexto =>
                            !contexto.Request.Path.StartsWithSegments("/health");
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(options =>
                        options.FilterHttpRequestMessage = requisicao =>
                            !EhTelemetriaSaindo(requisicao.RequestUri))
                    .AddSqlClientInstrumentation(options =>
                    {
                        // O texto do SQL (db.query.text) ja vem por padrao nesta versao — nao ha
                        // mais SetDbStatementForText para ligar. E o que torna o span do banco
                        // util e, ao mesmo tempo, o que exige atencao: em producao esse texto vai
                        // para o backend de traces, que aqui nao tem controle de acesso nenhum.
                        // Os VALORES dos parametros continuam fora, atras de variavel de ambiente
                        // experimental — e devem continuar assim.
                        options.RecordException = true;

                        // O readiness sonda o banco a cada 15s pelo publisher, fora de qualquer
                        // requisicao — entao o span nasce SEM pai e vira um trace inteiro so seu,
                        // "SELECT", para sempre. Filtrar a rota /health no ASP.NET Core nao pega
                        // este caso: ele nao passa por rota nenhuma.
                        options.Filter = comando =>
                            comando is not SqlCommand consulta
                            || consulta.CommandText != HealthCheckExtensions.ConsultaDeSaude;
                    })
                    // Sem este AddSource a fonte da Application existe, mas ninguem escuta:
                    // StartActivity devolve null e os spans de dominio simplesmente nao nascem.
                    .AddSource(CoreFinanceActivitySource.Name);

                tracing.AddOtlpExporter(options =>
                {
                    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    }
                });
            });

        return services;
    }

    /// <summary>
    /// Identifica as chamadas HTTP que a própria observabilidade faz, para não instrumentá-las.
    /// </summary>
    // O sink do Serilog empurra os logs para o Loki por HTTP. Com a instrumentacao de HttpClient
    // ligada, CADA push vira um trace — e como o sink dispara a cada poucos segundos, para sempre,
    // o Tempo enche de spans "POST http://loki:3100/loki/api/v1/push" que nao dizem nada sobre a
    // aplicacao. E o mesmo problema do /health, vindo de outro lado: telemetria observando a si
    // mesma. O OTLP entra na lista pelo mesmo motivo, para o caso de trocar gRPC por http/protobuf.
    private static bool EhTelemetriaSaindo(Uri? destino)
        => destino is not null
           && (destino.AbsolutePath.StartsWith("/loki/api/", StringComparison.Ordinal)
               || destino.AbsolutePath.StartsWith("/v1/traces", StringComparison.Ordinal)
               || destino.AbsolutePath.StartsWith("/v1/metrics", StringComparison.Ordinal)
               || destino.AbsolutePath.StartsWith("/v1/logs", StringComparison.Ordinal));

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
