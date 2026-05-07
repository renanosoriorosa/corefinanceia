namespace CoreFinance.Application.Payments.Dtos;

public class PaymentDto
{
    public Guid Id { get; init; }
    public Guid? FixedAccountId { get; init; }
    public string? FixedAccountName { get; init; }
    public string Description { get; init; } = null!;
    public decimal Amount { get; init; }
    public int Month { get; init; }
    public int Year { get; init; }
    public bool IsFixedAccount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
