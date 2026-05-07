namespace CoreFinance.Application.Dashboard.Dtos;

public class DashboardAnualDto
{
    public int Ano { get; init; }
    public decimal TotalAno { get; init; }
    public decimal MaiorMes { get; init; }
    public IEnumerable<MesValorDto> Meses { get; init; } = [];
}
