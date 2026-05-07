using CoreFinance.Domain.Common;

namespace CoreFinance.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid? FixedAccountId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public int Month { get; private set; }
    public int Year { get; private set; }
    public bool IsFixedAccount { get; private set; }

    public FixedAccount? FixedAccount { get; private set; }

    protected Payment() { }

    public Payment(string description, decimal amount, int month, int year, bool isFixedAccount, Guid? fixedAccountId = null)
    {
        Description = description;
        Amount = amount;
        Month = month;
        Year = year;
        IsFixedAccount = isFixedAccount;
        FixedAccountId = fixedAccountId;
    }

    public void Update(string description, decimal amount, int month, int year, bool isFixedAccount, Guid? fixedAccountId = null)
    {
        Description = description;
        Amount = amount;
        Month = month;
        Year = year;
        IsFixedAccount = isFixedAccount;
        FixedAccountId = fixedAccountId;
        SetUpdatedAt();
    }
}
