namespace CoreFinance.Domain.Interfaces;

/// <summary>
/// Expõe o usuário da requisição atual para as camadas que precisam
/// isolar dados por dono.
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    bool Autenticado { get; }
}
