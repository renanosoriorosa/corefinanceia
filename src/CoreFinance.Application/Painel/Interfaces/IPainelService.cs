using CoreFinance.Application.Common;
using CoreFinance.Application.Painel.Dtos;

namespace CoreFinance.Application.Painel.Interfaces;

public interface IPainelService
{
    Task<Result<PainelMesAtualDto>> ObterMesAtualAsync();
}
