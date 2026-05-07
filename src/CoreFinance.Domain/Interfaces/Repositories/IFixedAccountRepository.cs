using CoreFinance.Domain.Entities;

namespace CoreFinance.Domain.Interfaces.Repositories;

public interface IFixedAccountRepository : IBaseRepository<FixedAccount>
{
    Task<IEnumerable<FixedAccount>> ObterAtivasObrigatoriasAsync();
}
