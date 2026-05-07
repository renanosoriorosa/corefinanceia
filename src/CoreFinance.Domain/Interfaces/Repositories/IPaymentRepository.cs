using CoreFinance.Domain.Entities;

namespace CoreFinance.Domain.Interfaces.Repositories;

public interface IPaymentRepository : IBaseRepository<Payment>
{
    Task<IEnumerable<Payment>> ObterTodosComContaFixaAsync();
    Task<Payment?> ObterPorIdComContaFixaAsync(Guid id);
    Task<IEnumerable<Payment>> ObterPorMesAnoAsync(int mes, int ano);
    Task<IEnumerable<Payment>> ObterPorAnoAsync(int ano);
}
