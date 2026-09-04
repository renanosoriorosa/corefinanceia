using CoreFinance.Domain.Common;

namespace CoreFinance.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool Active { get; private set; }

    protected User() { }

    public User(string name, string email, string passwordHash)
    {
        Name = name;
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Active = true;
    }

    public void AtualizarNome(string name)
    {
        Name = name;
        SetUpdatedAt();
    }

    public void AtualizarSenha(string passwordHash)
    {
        PasswordHash = passwordHash;
        SetUpdatedAt();
    }

    public void Desativar()
    {
        Active = false;
        SetUpdatedAt();
    }
}
