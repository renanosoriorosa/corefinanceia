using CoreFinance.Application.Common;
using CoreFinance.Application.Dashboard.Dtos;
using CoreFinance.Application.Dashboard.Interfaces;
using CoreFinance.Domain.Entities;
using CoreFinance.Domain.Interfaces.Repositories;

namespace CoreFinance.Application.Dashboard.Services;

public class DashboardService : IDashboardService
{
    private const int MesesNaMediaMovel = 3;
    private const int MaximoContasNaDistribuicao = 6;
    private const int QuantidadeTopGastos = 5;
    private const string ContaAvulsa = "Avulsas";

    private readonly IPaymentRepository _paymentRepository;

    public DashboardService(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<DashboardAnualDto>> ObterAnualAsync(int ano, Guid? contaFixaId, bool incluirNaoFixas)
    {
        var lancamentos = Filtrar(await _paymentRepository.ObterPorAnoAsync(ano), contaFixaId, incluirNaoFixas);
        var lancamentosAnteriores = Filtrar(await _paymentRepository.ObterPorAnoAsync(ano - 1), contaFixaId, incluirNaoFixas);

        var totais = TotalPorMes(lancamentos);
        var totaisAnteriores = TotalPorMes(lancamentosAnteriores);

        var mediaMensal = MediaDosMesesComLancamento(totais);
        var ultimoMesRealizado = UltimoMesRealizado(ano);
        var primeiroMesComLancamento = PrimeiroMesComLancamento(totais);

        var meses = Enumerable.Range(1, 12)
            .Select(mes => new MesValorDto
            {
                Mes = mes,
                Total = totais[mes],
                MediaMovel = MediaMovel(totais, mes, ultimoMesRealizado, primeiroMesComLancamento),
                Projetado = Projetado(mes, ultimoMesRealizado, mediaMensal)
            })
            .ToList();

        return Result<DashboardAnualDto>.Ok(new DashboardAnualDto
        {
            Ano = ano,
            TotalAno = Arredondar(lancamentos.Sum(p => p.Amount)),
            MaiorMes = Arredondar(meses.Max(m => m.Total)),
            MediaMensal = mediaMensal,
            ProjecaoAno = ProjecaoAno(totais, ultimoMesRealizado, mediaMensal),
            Meses = meses,
            Comparativo = MontarComparativo(ano, totais, totaisAnteriores, ultimoMesRealizado),
            PorConta = DistribuirPorConta(lancamentos),
            TopGastos = SelecionarTopGastos(lancamentos)
        });
    }

    private static List<Payment> Filtrar(IEnumerable<Payment> pagamentos, Guid? contaFixaId, bool incluirNaoFixas)
    {
        if (contaFixaId.HasValue)
            return pagamentos.Where(p => p.FixedAccountId == contaFixaId).ToList();

        if (!incluirNaoFixas)
            return pagamentos.Where(p => p.IsFixedAccount).ToList();

        return pagamentos.ToList();
    }

    /// <summary>Totais indexados pelo número do mês (posição 0 não é usada).</summary>
    private static decimal[] TotalPorMes(IEnumerable<Payment> pagamentos)
    {
        var totais = new decimal[13];

        foreach (var pagamento in pagamentos.Where(p => p.Month is >= 1 and <= 12))
            totais[pagamento.Month] += pagamento.Amount;

        return totais;
    }

    /// <summary>
    /// Último mês que já aconteceu: 12 em anos fechados, o mês corrente no ano atual
    /// e 0 em anos futuros (nada realizado ainda).
    /// </summary>
    private static int UltimoMesRealizado(int ano)
    {
        var hoje = DateTime.Today;

        if (ano < hoje.Year) return 12;
        if (ano > hoje.Year) return 0;

        return hoje.Month;
    }

    /// <summary>Meses sem lançamento ficam de fora para não achatar a média.</summary>
    private static decimal MediaDosMesesComLancamento(decimal[] totais)
    {
        var comLancamento = totais.Skip(1).Where(t => t > 0).ToList();

        return comLancamento.Count == 0 ? 0m : Arredondar(comLancamento.Average());
    }

    private static int? PrimeiroMesComLancamento(decimal[] totais)
    {
        for (var mes = 1; mes <= 12; mes++)
            if (totais[mes] > 0)
                return mes;

        return null;
    }

    /// <summary>
    /// Média dos três últimos meses. Só existe quando a janela inteira já aconteceu e
    /// começa depois do primeiro lançamento — antes disso os zeros distorceriam a curva.
    /// </summary>
    private static decimal? MediaMovel(decimal[] totais, int mes, int ultimoMesRealizado, int? primeiroMesComLancamento)
    {
        if (primeiroMesComLancamento is null || mes > ultimoMesRealizado)
            return null;

        var inicioDaJanela = mes - MesesNaMediaMovel + 1;

        if (inicioDaJanela < primeiroMesComLancamento)
            return null;

        var soma = 0m;
        for (var i = inicioDaJanela; i <= mes; i++)
            soma += totais[i];

        return Arredondar(soma / MesesNaMediaMovel);
    }

    private static decimal? Projetado(int mes, int ultimoMesRealizado, decimal mediaMensal)
        => mes > ultimoMesRealizado && ultimoMesRealizado > 0 && mediaMensal > 0 ? mediaMensal : null;

    /// <summary>Realizado até hoje somado à média aplicada aos meses que faltam fechar.</summary>
    private static decimal ProjecaoAno(decimal[] totais, int ultimoMesRealizado, decimal mediaMensal)
    {
        var realizado = totais.Skip(1).Take(ultimoMesRealizado).Sum();

        if (ultimoMesRealizado is 0 or 12)
            return Arredondar(totais.Skip(1).Sum());

        return Arredondar(realizado + mediaMensal * (12 - ultimoMesRealizado));
    }

    private static ComparativoDto MontarComparativo(int ano, decimal[] totais, decimal[] totaisAnteriores, int ultimoMesRealizado)
    {
        var mesReferencia = ultimoMesRealizado > 0
            ? ultimoMesRealizado
            : UltimoMesComLancamento(totais) ?? 1;

        var totalMesReferencia = totais[mesReferencia];

        // Em janeiro o mês anterior é dezembro do ano passado.
        var totalMesAnterior = mesReferencia > 1 ? totais[mesReferencia - 1] : totaisAnteriores[12];

        var acumulado = totais.Skip(1).Take(mesReferencia).Sum();
        var acumuladoAnterior = totaisAnteriores.Skip(1).Take(mesReferencia).Sum();

        return new ComparativoDto
        {
            MesReferencia = mesReferencia,
            TotalMesReferencia = Arredondar(totalMesReferencia),
            TotalMesAnterior = Arredondar(totalMesAnterior),
            VariacaoMensalPercentual = Variacao(totalMesReferencia, totalMesAnterior),
            TotalAcumuladoAno = Arredondar(acumulado),
            TotalAcumuladoAnoAnterior = Arredondar(acumuladoAnterior),
            VariacaoAnualPercentual = Variacao(acumulado, acumuladoAnterior)
        };
    }

    private static int? UltimoMesComLancamento(decimal[] totais)
    {
        for (var mes = 12; mes >= 1; mes--)
            if (totais[mes] > 0)
                return mes;

        return null;
    }

    /// <summary>Sem base de comparação (período anterior zerado) a variação percentual não existe.</summary>
    private static decimal? Variacao(decimal atual, decimal anterior)
        => anterior > 0 ? Arredondar((atual - anterior) / anterior * 100) : null;

    /// <summary>
    /// Distribuição por conta fixa. As contas menores viram uma fatia "Outras"
    /// para a pizza não ficar ilegível.
    /// </summary>
    private static List<ContaValorDto> DistribuirPorConta(List<Payment> pagamentos)
    {
        var total = pagamentos.Sum(p => p.Amount);

        if (total <= 0)
            return [];

        var agrupado = pagamentos
            .GroupBy(NomeDaConta)
            .Select(g => (Nome: g.Key, Total: g.Sum(p => p.Amount)))
            .OrderByDescending(x => x.Total)
            .ToList();

        var fatias = agrupado.Take(MaximoContasNaDistribuicao).ToList();
        var restantes = agrupado.Skip(MaximoContasNaDistribuicao).ToList();

        if (restantes.Count > 0)
            fatias.Add(("Outras", restantes.Sum(x => x.Total)));

        return fatias
            .Select(x => new ContaValorDto
            {
                Nome = x.Nome,
                Total = Arredondar(x.Total),
                Percentual = Arredondar(x.Total / total * 100)
            })
            .ToList();
    }

    private static List<TopGastoDto> SelecionarTopGastos(List<Payment> pagamentos)
        => pagamentos
            .OrderByDescending(p => p.Amount)
            .ThenBy(p => p.Month)
            .Take(QuantidadeTopGastos)
            .Select(p => new TopGastoDto
            {
                Id = p.Id,
                Descricao = p.Description,
                Conta = NomeDaConta(p),
                Mes = p.Month,
                Valor = Arredondar(p.Amount)
            })
            .ToList();

    private static string NomeDaConta(Payment pagamento) => pagamento.FixedAccount?.Name ?? ContaAvulsa;

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
