namespace CoreFinance.Application.Dashboard.Dtos;

public class TopGastoDto
{
    public Guid Id { get; init; }
    public string Descricao { get; init; } = null!;
    public string Conta { get; init; } = null!;
    public int Mes { get; init; }
    public decimal Valor { get; init; }
}
