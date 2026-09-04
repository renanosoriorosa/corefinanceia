using CoreFinance.Application.Common;
using CoreFinance.Application.Payments.Dtos;
using CoreFinance.Application.Payments.Interfaces;
using CoreFinance.Domain.Entities;
using CoreFinance.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CoreFinance.Application.Payments.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _repository;
    private readonly IFixedAccountRepository _fixedAccountRepository;
    private readonly IValidator<CreatePaymentRequest> _createValidator;
    private readonly IValidator<UpdatePaymentRequest> _updateValidator;

    public PaymentService(
        IPaymentRepository repository,
        IFixedAccountRepository fixedAccountRepository,
        IValidator<CreatePaymentRequest> createValidator,
        IValidator<UpdatePaymentRequest> updateValidator)
    {
        _repository = repository;
        _fixedAccountRepository = fixedAccountRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<Result<IEnumerable<PaymentDto>>> ObterTodosAsync()
    {
        var pagamentos = await _repository.ObterTodosComContaFixaAsync();
        return Result<IEnumerable<PaymentDto>>.Ok(pagamentos.Select(ToDto));
    }

    public async Task<Result<IEnumerable<PaymentDto>>> ObterPorMesAnoAsync(int mes, int ano)
    {
        var pagamentos = await _repository.ObterPorMesAnoAsync(mes, ano);
        return Result<IEnumerable<PaymentDto>>.Ok(pagamentos.Select(ToDto));
    }

    public async Task<Result<PaymentDto>> ObterPorIdAsync(Guid id)
    {
        var pagamento = await _repository.ObterPorIdComContaFixaAsync(id);
        if (pagamento is null)
            return Result<PaymentDto>.Fail("Pagamento não encontrado.");

        return Result<PaymentDto>.Ok(ToDto(pagamento));
    }

    public async Task<Result<PaymentDto>> CriarAsync(CreatePaymentRequest request)
    {
        var validacao = await _createValidator.ValidateAsync(request);
        if (!validacao.IsValid)
            return Result<PaymentDto>.Fail(validacao.Errors[0].ErrorMessage);

        if (!await ContaFixaValidaAsync(request.FixedAccountId))
            return Result<PaymentDto>.Fail("Conta fixa não encontrada.");

        var pagamento = new Payment(
            request.Description,
            request.Amount,
            request.Month,
            request.Year,
            request.IsFixedAccount,
            request.FixedAccountId);

        await _repository.AdicionarAsync(pagamento);
        await _repository.SalvarAsync();

        var criado = await _repository.ObterPorIdComContaFixaAsync(pagamento.Id);
        return Result<PaymentDto>.Ok(ToDto(criado!));
    }

    public async Task<Result<PaymentDto>> AtualizarAsync(Guid id, UpdatePaymentRequest request)
    {
        var validacao = await _updateValidator.ValidateAsync(request);
        if (!validacao.IsValid)
            return Result<PaymentDto>.Fail(validacao.Errors[0].ErrorMessage);

        var pagamento = await _repository.ObterPorIdAsync(id);
        if (pagamento is null)
            return Result<PaymentDto>.Fail("Pagamento não encontrado.");

        if (!await ContaFixaValidaAsync(request.FixedAccountId))
            return Result<PaymentDto>.Fail("Conta fixa não encontrada.");

        pagamento.Update(
            request.Description,
            request.Amount,
            request.Month,
            request.Year,
            request.IsFixedAccount,
            request.FixedAccountId);

        await _repository.AtualizarAsync(pagamento);
        await _repository.SalvarAsync();

        var atualizado = await _repository.ObterPorIdComContaFixaAsync(pagamento.Id);
        return Result<PaymentDto>.Ok(ToDto(atualizado!));
    }

    public async Task<Result> RemoverAsync(Guid id)
    {
        var pagamento = await _repository.ObterPorIdAsync(id);
        if (pagamento is null)
            return Result.Fail("Pagamento não encontrado.");

        await _repository.RemoverAsync(pagamento);
        await _repository.SalvarAsync();

        return Result.Ok();
    }

    /// <summary>
    /// Garante que a conta fixa informada existe e pertence ao usuário logado
    /// (o repositório já enxerga apenas os registros do dono).
    /// </summary>
    private async Task<bool> ContaFixaValidaAsync(Guid? contaFixaId)
    {
        if (!contaFixaId.HasValue)
            return true;

        return await _fixedAccountRepository.ObterPorIdAsync(contaFixaId.Value) is not null;
    }

    private static PaymentDto ToDto(Payment p) => new()
    {
        Id = p.Id,
        FixedAccountId = p.FixedAccountId,
        FixedAccountName = p.FixedAccount?.Name,
        Description = p.Description,
        Amount = p.Amount,
        Month = p.Month,
        Year = p.Year,
        IsFixedAccount = p.IsFixedAccount,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
