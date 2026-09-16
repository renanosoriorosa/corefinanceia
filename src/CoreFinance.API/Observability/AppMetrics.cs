using System.Diagnostics;
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

    // Estado da conta de CPU: utilizacao so existe entre DUAS leituras, entao o instrumento
    // precisa lembrar onde estava na anterior. Mesmo motivo do lock acima — quem coleta e uma
    // thread do OpenTelemetry, e duas coletas concorrentes embaralhariam o delta.
    private readonly Process _processo = Process.GetCurrentProcess();
    private readonly object _travaCpu = new();
    private TimeSpan _cpuAcumuladaAnterior;
    private long _timestampAnterior;
    private double _utilizacaoCpu;

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

        // CPU nao vem de graca: a Instrumentation.Runtime nao publica utilizacao, e o pacote
        // OpenTelemetry.Instrumentation.Process, que publicaria, nunca saiu de pre-release —
        // e este projeto so usa OTel estavel. Entao a conta e feita aqui, e ela e a definicao
        // de utilizacao: tempo de CPU consumido / (tempo de relogio x nucleos disponiveis).
        _cpuAcumuladaAnterior = _processo.TotalProcessorTime;
        _timestampAnterior = Stopwatch.GetTimestamp();

        _meter.CreateObservableGauge(
            name: "corefinance.process.cpu.utilization",
            observeValue: LerUsoDeCpu,
            unit: "1",
            description: "Fracao de CPU usada pelo processo: 1 = todos os nucleos saturados.");

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

    // Utilizacao e sempre uma media sobre um intervalo — aqui, o intervalo entre esta coleta e
    // a anterior (OTEL_METRIC_EXPORT_INTERVAL, 15s no compose). Nao existe "uso de CPU agora".
    private double LerUsoDeCpu()
    {
        lock (_travaCpu)
        {
            var agora = Stopwatch.GetTimestamp();
            var decorrido = Stopwatch.GetElapsedTime(_timestampAnterior, agora);

            // Duas coletas quase coladas (um segundo reader, ou um scrape manual no meio do
            // ciclo) dividiriam por um intervalo minusculo e desenhariam um pico que nunca
            // existiu. Abaixo de um segundo, repete o ultimo valor em vez de inventar um.
            if (decorrido < TimeSpan.FromSeconds(1))
            {
                return _utilizacaoCpu;
            }

            _processo.Refresh();
            var cpuAcumulada = _processo.TotalProcessorTime;

            // ProcessorCount respeita o limite de CPU do container, nao o da maquina — entao o
            // 1.0 aqui significa "saturei o que me deram", que e a leitura que interessa.
            _utilizacaoCpu = (cpuAcumulada - _cpuAcumuladaAnterior).TotalSeconds
                / (decorrido.TotalSeconds * Environment.ProcessorCount);

            _cpuAcumuladaAnterior = cpuAcumulada;
            _timestampAnterior = agora;

            return _utilizacaoCpu;
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
        _processo.Dispose();
    }
}
