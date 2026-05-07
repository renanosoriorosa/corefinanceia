using CoreFinance.Application.Common;
using CoreFinance.Application.Painel.Dtos;
using CoreFinance.Application.Painel.Interfaces;
using CoreFinance.Domain.Interfaces.Repositories;

namespace CoreFinance.Application.Painel.Services;

public class PainelService : IPainelService
{
    private readonly IFixedAccountRepository _fixedAccountRepository;
    private readonly IPaymentRepository _paymentRepository;

    public PainelService(IFixedAccountRepository fixedAccountRepository, IPaymentRepository paymentRepository)
    {
        _fixedAccountRepository = fixedAccountRepository;
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<PainelMesAtualDto>> ObterMesAtualAsync()
    {
        var hoje = DateTime.Today;
        var mes = hoje.Month;
        var ano = hoje.Year;

        var contasObrigatorias = await _fixedAccountRepository.ObterAtivasObrigatoriasAsync();
        var pagamentos = await _paymentRepository.ObterPorMesAnoAsync(mes, ano);

        var pagas = new List<ContaPainelDto>();
        var pendentes = new List<ContaPainelDto>();

        foreach (var conta in contasObrigatorias)
        {
            var pagamento = pagamentos.FirstOrDefault(p => p.FixedAccountId == conta.Id);

            if (pagamento is not null)
            {
                pagas.Add(new ContaPainelDto
                {
                    ContaFixaId = conta.Id,
                    Nome = conta.Name,
                    Descricao = conta.Description,
                    PagamentoId = pagamento.Id,
                    ValorPago = pagamento.Amount
                });
            }
            else
            {
                pendentes.Add(new ContaPainelDto
                {
                    ContaFixaId = conta.Id,
                    Nome = conta.Name,
                    Descricao = conta.Description
                });
            }
        }

        var totalPago = pagamentos.Sum(p => p.Amount);

        return Result<PainelMesAtualDto>.Ok(new PainelMesAtualDto
        {
            Mes = mes,
            Ano = ano,
            TotalPago = totalPago,
            TotalPendentes = pendentes.Count,
            ContasPagas = pagas,
            ContasPendentes = pendentes
        });
    }
}
