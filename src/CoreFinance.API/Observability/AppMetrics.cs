using System.Diagnostics.Metrics;
using CoreFinance.Application.Common.Observability;

namespace CoreFinance.API.Observability;

/// <summary>
/// Implementação das métricas próprias da aplicação. Registrada como <b>singleton</b>: o
/// <see cref="Meter"/> e seus instrumentos vivem enquanto o processo viver.
/// </summary>
// Nada aqui depende do exportador estar ligado. Com Observability:Enabled=false o Meter
// continua existindo e ninguem escuta — medir e barato, exportar e que custa.
public sealed class AppMetrics : IAppMetrics, IDisposable
{
    /// <summary>Nome do meter. Precisa bater com o <c>AddMeter</c> em <see cref="ObservabilityExtensions"/>.</summary>
    public const string MeterName = "CoreFinance.App";

    private readonly Meter _meter;
    private readonly Counter<long> _pagamentosCriados;

    // Escrito pelo HealthMetricsPublisher (uma thread do publisher) e lido pela thread de
    // coleta do OpenTelemetry. double nao tem leitura/escrita atomica garantida em 32 bits,
    // entao o acesso passa por lock — sao duas operacoes a cada 15s, o custo e irrelevante.
    private readonly object _travaSaude = new();
    private double? _saudeGeral;
    private IReadOnlyList<KeyValuePair<string, double>> _saudePorCheck = [];

    public AppMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _pagamentosCriados = _meter.CreateCounter<long>(
            name: "corefinance.payments.created",
            unit: "{payment}",
            description: "Pagamentos criados.");

        // Gauge = valor que sobe e desce, lido sob demanda pelo SDK (por isso "Observable":
        // quem coleta chama o callback, em vez de a aplicacao empurrar o valor).
        // Healthy = 1 | Degraded = 0.5 | Unhealthy = 0.
        _meter.CreateObservableGauge(
            name: "corefinance.health.status",
            observeValues: LerSaudeGeral,
            unit: "{status}",
            description: "Saude agregada da API: 1 saudavel, 0.5 degradada, 0 fora.");

        _meter.CreateObservableGauge(
            name: "corefinance.health.check.status",
            observeValues: LerSaudePorCheck,
            unit: "{status}",
            description: "Saude de cada health check individual.");
    }

    public void PagamentoCriado(bool contaFixa) =>
        // is_fixed_account tem 2 valores possiveis. Usar algo como user_id aqui criaria uma
        // serie temporal por usuario — cardinalidade alta mata o Prometheus em RAM.
        _pagamentosCriados.Add(1, new KeyValuePair<string, object?>("is_fixed_account", contaFixa));

    /// <summary>
    /// Recebe o resultado mais recente dos health checks. Chamado pelo
    /// <c>HealthMetricsPublisher</c> no período configurado.
    /// </summary>
    public void AtualizarSaude(double geral, IReadOnlyList<KeyValuePair<string, double>> porCheck)
    {
        lock (_travaSaude)
        {
            _saudeGeral = geral;
            _saudePorCheck = porCheck;
        }
    }

    // Antes do primeiro ciclo do publisher nao existe medicao alguma — devolver 0 aqui seria
    // mentir que a API esta fora do ar durante a partida. Sem valor, nenhuma amostra e emitida.
    private IEnumerable<Measurement<double>> LerSaudeGeral()
    {
        lock (_travaSaude)
        {
            return _saudeGeral is null ? [] : [new Measurement<double>(_saudeGeral.Value)];
        }
    }

    private IEnumerable<Measurement<double>> LerSaudePorCheck()
    {
        lock (_travaSaude)
        {
            return _saudePorCheck
                .Select(c => new Measurement<double>(
                    c.Value,
                    new KeyValuePair<string, object?>("health_check", c.Key)))
                .ToArray();
        }
    }

    public void Dispose() => _meter.Dispose();
}
