namespace CoreFinance.Application.Common.Observability;

/// <summary>
/// Porta de métricas de negócio. A Application declara <b>o que</b> vale medir; quem sabe
/// <b>como</b> medir (OpenTelemetry, <c>System.Diagnostics.Metrics</c>) é a camada de API.
/// Mesma inversão de dependência já usada em <c>ICurrentUser</c>.
/// </summary>
public interface IAppMetrics
{
    /// <summary>
    /// Registra a criação de um pagamento.
    /// </summary>
    /// <param name="contaFixa">Se o pagamento veio de uma conta fixa. Vira dimensão da métrica.</param>
    void PagamentoCriado(bool contaFixa);
}
