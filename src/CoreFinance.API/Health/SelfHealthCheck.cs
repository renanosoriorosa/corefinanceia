using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoreFinance.API.Health;

// Check de liveness: nao consulta dependencia alguma.
// Responder ja prova que o processo subiu e que o pipeline de requisicao esta atendendo.
public sealed class SelfHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy("API respondendo."));
}
