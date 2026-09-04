namespace CoreFinance.Application.Dashboard.Dtos;

/// <summary>
/// Comparações do mês de referência com o mês anterior e do ano com o anterior,
/// sempre no mesmo intervalo (janeiro até o mês de referência) para a comparação ser justa.
/// </summary>
public class ComparativoDto
{
    public int MesReferencia { get; init; }
    public decimal TotalMesReferencia { get; init; }
    public decimal TotalMesAnterior { get; init; }

    /// <summary>Nulo quando o mês anterior não teve lançamento (variação percentual não faz sentido).</summary>
    public decimal? VariacaoMensalPercentual { get; init; }

    public decimal TotalAcumuladoAno { get; init; }
    public decimal TotalAcumuladoAnoAnterior { get; init; }
    public decimal? VariacaoAnualPercentual { get; init; }
}
