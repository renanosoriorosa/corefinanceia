namespace CoreFinance.Application.FixedAccounts.Dtos;

public class FixedAccountDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public bool IsRequiredMonthly { get; init; }
    public bool Active { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
