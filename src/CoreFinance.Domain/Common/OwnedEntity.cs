namespace CoreFinance.Domain.Common;

/// <summary>
/// Entidade que pertence a um usuário. O preenchimento do dono é feito
/// automaticamente pelo contexto ao salvar novos registros.
/// </summary>
public abstract class OwnedEntity : BaseEntity
{
    public Guid UserId { get; protected set; }

    public void DefinirDono(Guid userId) => UserId = userId;
}
