using CoreFinance.Application.Common;
using CoreFinance.Application.FixedAccounts.Dtos;

namespace CoreFinance.Application.FixedAccounts.Interfaces;

public interface IFixedAccountService
{
    Task<Result<IEnumerable<FixedAccountDto>>> ObterTodosAsync();
    Task<Result<FixedAccountDto>> ObterPorIdAsync(Guid id);
    Task<Result<FixedAccountDto>> CriarAsync(CreateFixedAccountRequest request);
    Task<Result<FixedAccountDto>> AtualizarAsync(Guid id, UpdateFixedAccountRequest request);
    Task<Result> RemoverAsync(Guid id);
}
