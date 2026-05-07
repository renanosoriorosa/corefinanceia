namespace CoreFinance.Application.FixedAccounts.Dtos;

public class CreateFixedAccountRequest
{
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public bool IsRequiredMonthly { get; init; }
    public bool Active { get; init; } = true;
}
