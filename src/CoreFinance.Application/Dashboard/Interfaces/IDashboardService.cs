using CoreFinance.Application.Common;
using CoreFinance.Application.Dashboard.Dtos;

namespace CoreFinance.Application.Dashboard.Interfaces;

public interface IDashboardService
{
    Task<Result<DashboardAnualDto>> ObterAnualAsync(int ano, Guid? contaFixaId, bool incluirNaoFixas);
}
