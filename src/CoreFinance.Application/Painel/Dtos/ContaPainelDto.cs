namespace CoreFinance.Application.Painel.Dtos;

public class ContaPainelDto
{
    public Guid ContaFixaId { get; init; }
    public string Nome { get; init; } = null!;
    public string? Descricao { get; init; }
    public Guid? PagamentoId { get; init; }
    public decimal? ValorPago { get; init; }
}
