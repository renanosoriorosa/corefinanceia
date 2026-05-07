using CoreFinance.Domain.Entities;
using CoreFinance.Domain.Interfaces.Repositories;
using CoreFinance.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace CoreFinance.Infra.Repositories;

public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Payment>> ObterTodosComContaFixaAsync()
        => await _dbSet
            .AsNoTracking()
            .Include(p => p.FixedAccount)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .ToListAsync();

    public async Task<Payment?> ObterPorIdComContaFixaAsync(Guid id)
        => await _dbSet
            .Include(p => p.FixedAccount)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IEnumerable<Payment>> ObterPorMesAnoAsync(int mes, int ano)
        => await _dbSet
            .AsNoTracking()
            .Include(p => p.FixedAccount)
            .Where(p => p.Month == mes && p.Year == ano)
            .ToListAsync();

    public async Task<IEnumerable<Payment>> ObterPorAnoAsync(int ano)
        => await _dbSet
            .AsNoTracking()
            .Include(p => p.FixedAccount)
            .Where(p => p.Year == ano)
            .ToListAsync();
}
