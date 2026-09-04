namespace CoreFinance.Application.Dashboard.Dtos;

public class MesValorDto
{
    public int Mes { get; init; }
    public decimal Total { get; init; }

    /// <summary>Média dos últimos três meses. Nulo quando não há janela suficiente ou dados.</summary>
    public decimal? MediaMovel { get; init; }

    /// <summary>Valor estimado para meses do ano corrente que ainda não aconteceram.</summary>
    public decimal? Projetado { get; init; }
}
