using CoreFinance.Domain.Interfaces.Security;

namespace CoreFinance.Infra.Security;

public class PasswordHasher : IPasswordHasher
{
    public string Gerar(string senha)
        => BCrypt.Net.BCrypt.HashPassword(senha);

    public bool Verificar(string senha, string hash)
        => BCrypt.Net.BCrypt.Verify(senha, hash);
}
