using CoreFinance.Application.Common;
using CoreFinance.Application.Payments.Dtos;

namespace CoreFinance.Application.Payments.Interfaces;

public interface IPaymentService
{
    Task<Result<IEnumerable<PaymentDto>>> ObterTodosAsync();
    Task<Result<IEnumerable<PaymentDto>>> ObterPorMesAnoAsync(int mes, int ano);
    Task<Result<PaymentDto>> ObterPorIdAsync(Guid id);
    Task<Result<PaymentDto>> CriarAsync(CreatePaymentRequest request);
    Task<Result<PaymentDto>> AtualizarAsync(Guid id, UpdatePaymentRequest request);
    Task<Result> RemoverAsync(Guid id);
}
