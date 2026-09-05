using CoreFinance.API.Health;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoreFinance.API.Extensions;

public static class HealthCheckExtensions
{
    private const int TimeoutPadraoEmSegundos = 5;

    public static IServiceCollection AddHealthChecksConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var segundos = configuration.GetValue<int?>("HealthChecks:SqlServer:TimeoutEmSegundos")
            ?? TimeoutPadraoEmSegundos;
        var timeoutSqlServer = TimeSpan.FromSeconds(segundos);

        services.AddHealthChecks()
            .AddCheck<SelfHealthCheck>(
                name: "self",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["live"])
            .AddSqlServer(
                connectionString: MontarConnectionStringDeHealthCheck(
                    configuration.GetConnectionString("DefaultConnection")!, segundos),
                healthQuery: "SELECT 1;",
                name: "sqlserver",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "db"],
                timeout: timeoutSqlServer);

        // Preparado para quando existirem — a tag "ready" ja os inclui no readiness,
        // sem tocar no mapeamento abaixo:
        // .AddRedis(...)     tags: ["ready", "cache"]
        // .AddRabbitMQ(...)  tags: ["ready", "mq"]
        // .AddUrlGroup(...)  tags: ["ready", "external"]

        return services;
    }

    // O timeout do registro de health check nao consegue abortar um SqlConnection.OpenAsync
    // travado: o SqlClient so desiste quando estoura o proprio Connect Timeout (15s por padrao).
    // Sem isto, /health/ready fica ~15s pendurado com o banco fora do ar.
    private static string MontarConnectionStringDeHealthCheck(string connectionString, int timeoutEmSegundos)
        => new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = timeoutEmSegundos,
            ConnectRetryCount = 0
        }.ConnectionString;

    public static WebApplication MapHealthChecksConfig(this WebApplication app)
    {
        // Visao completa: todos os checks, com detalhe de cada um.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        // Liveness: o processo esta vivo? Nao depende de nada externo,
        // porque reiniciar o container nao conserta um banco fora do ar.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        // Readiness: da para mandar trafego? Aqui sim as dependencias contam.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        return app;
    }
}
