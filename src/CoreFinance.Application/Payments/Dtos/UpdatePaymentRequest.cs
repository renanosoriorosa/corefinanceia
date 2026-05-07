namespace CoreFinance.Application.Payments.Dtos;

public class UpdatePaymentRequest
{
    public Guid? FixedAccountId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Amount { get; init; }
    public int Month { get; init; }
    public int Year { get; init; }
    public bool IsFixedAccount { get; init; }
}
