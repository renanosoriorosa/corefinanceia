namespace CoreFinance.Application.Painel.Dtos;

public class PainelMesAtualDto
{
    public int Mes { get; init; }
    public int Ano { get; init; }
    public decimal TotalPago { get; init; }
    public int TotalPendentes { get; init; }
    public IEnumerable<ContaPainelDto> ContasPagas { get; init; } = [];
    public IEnumerable<ContaPainelDto> ContasPendentes { get; init; } = [];
}
