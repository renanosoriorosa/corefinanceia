namespace CoreFinance.Application.Dashboard.Dtos;

public class DashboardAnualDto
{
    public int Ano { get; init; }
    public decimal TotalAno { get; init; }
    public decimal MaiorMes { get; init; }

    /// <summary>Média dos meses que tiveram lançamento. Meses zerados não entram no cálculo.</summary>
    public decimal MediaMensal { get; init; }

    /// <summary>Realizado + média projetada nos meses restantes. No ano fechado é igual ao total.</summary>
    public decimal ProjecaoAno { get; init; }

    public IEnumerable<MesValorDto> Meses { get; init; } = [];
    public ComparativoDto Comparativo { get; init; } = new();
    public IEnumerable<ContaValorDto> PorConta { get; init; } = [];
    public IEnumerable<TopGastoDto> TopGastos { get; init; } = [];
}
