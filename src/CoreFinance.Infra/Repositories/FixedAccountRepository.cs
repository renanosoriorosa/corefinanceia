using CoreFinance.Domain.Entities;
using CoreFinance.Domain.Interfaces.Repositories;
using CoreFinance.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace CoreFinance.Infra.Repositories;

public class FixedAccountRepository : BaseRepository<FixedAccount>, IFixedAccountRepository
{
    public FixedAccountRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<FixedAccount>> ObterAtivasObrigatoriasAsync()
        => await _dbSet
            .AsNoTracking()
            .Where(c => c.Active && c.IsRequiredMonthly)
            .OrderBy(c => c.Name)
            .ToListAsync();
}
