using CoreFinance.Domain.Entities;

namespace CoreFinance.Domain.Interfaces.Repositories;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> ObterPorEmailAsync(string email);
    Task<bool> EmailExisteAsync(string email);
}
