namespace CoreFinance.Application.Dashboard.Dtos;

public class ContaValorDto
{
    public string Nome { get; init; } = null!;
    public decimal Total { get; init; }
    public decimal Percentual { get; init; }
}
