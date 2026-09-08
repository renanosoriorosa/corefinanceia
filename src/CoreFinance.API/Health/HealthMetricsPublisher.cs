using CoreFinance.API.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoreFinance.API.Health;

/// <summary>
/// Publica o resultado dos health checks como métrica. É o que transforma "está saudável agora?"
/// (fase 01) em série temporal — dá para ver <b>quando</b> ficou ruim e <b>por quanto tempo</b>,
/// e alertar sobre isso sem o Grafana precisar bater HTTP na API.
/// </summary>
// O runtime chama PublishAsync no periodo configurado em HealthCheckPublisherOptions, com o
// relatorio ja pronto: ninguem executa os checks duas vezes.
public sealed class HealthMetricsPublisher : IHealthCheckPublisher
{
    private readonly AppMetrics _metrics;

    public HealthMetricsPublisher(AppMetrics metrics) => _metrics = metrics;

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var porCheck = report.Entries
            .Select(e => new KeyValuePair<string, double>(e.Key, ParaValor(e.Value.Status)))
            .ToArray();

        _metrics.AtualizarSaude(ParaValor(report.Status), porCheck);

        return Task.CompletedTask;
    }

    private static double ParaValor(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => 1,
        HealthStatus.Degraded => 0.5,
        _ => 0
    };
}
