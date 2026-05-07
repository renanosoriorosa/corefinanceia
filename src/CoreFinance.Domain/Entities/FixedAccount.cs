using CoreFinance.Domain.Common;

namespace CoreFinance.Domain.Entities;

public class FixedAccount : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsRequiredMonthly { get; private set; }
    public bool Active { get; private set; }

    protected FixedAccount() { }

    public FixedAccount(string name, string? description, bool isRequiredMonthly, bool active = true)
    {
        Name = name;
        Description = description;
        IsRequiredMonthly = isRequiredMonthly;
        Active = active;
    }

    public void Update(string name, string? description, bool isRequiredMonthly, bool active)
    {
        Name = name;
        Description = description;
        IsRequiredMonthly = isRequiredMonthly;
        Active = active;
        SetUpdatedAt();
    }
}
