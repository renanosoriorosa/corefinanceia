using CoreFinance.Application.Common;
using CoreFinance.Application.Dashboard.Dtos;
using CoreFinance.Application.Dashboard.Interfaces;
using CoreFinance.Domain.Interfaces.Repositories;

namespace CoreFinance.Application.Dashboard.Services;

public class DashboardService : IDashboardService
{
    private readonly IPaymentRepository _paymentRepository;

    public DashboardService(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<DashboardAnualDto>> ObterAnualAsync(int ano, Guid? contaFixaId, bool incluirNaoFixas)
    {
        var todos = await _paymentRepository.ObterPorAnoAsync(ano);

        var filtrados = todos.AsEnumerable();

        if (contaFixaId.HasValue)
        {
            filtrados = filtrados.Where(p => p.FixedAccountId == contaFixaId);
        }
        else if (!incluirNaoFixas)
        {
            filtrados = filtrados.Where(p => p.IsFixedAccount);
        }

        var lista = filtrados.ToList();

        var agrupado = lista
            .GroupBy(p => p.Month)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var meses = Enumerable.Range(1, 12).Select(m => new MesValorDto
        {
            Mes = m,
            Total = agrupado.GetValueOrDefault(m, 0m)
        }).ToList();

        return Result<DashboardAnualDto>.Ok(new DashboardAnualDto
        {
            Ano = ano,
            TotalAno = lista.Sum(p => p.Amount),
            MaiorMes = meses.Max(m => m.Total),
            Meses = meses
        });
    }
}
