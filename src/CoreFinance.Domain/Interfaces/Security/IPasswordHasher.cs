namespace CoreFinance.Domain.Interfaces.Security;

public interface IPasswordHasher
{
    string Gerar(string senha);
    bool Verificar(string senha, string hash);
}
