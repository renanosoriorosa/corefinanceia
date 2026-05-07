using CoreFinance.Application.Common;
using CoreFinance.Application.FixedAccounts.Dtos;
using CoreFinance.Application.FixedAccounts.Interfaces;
using CoreFinance.Domain.Entities;
using CoreFinance.Domain.Interfaces.Repositories;
using FluentValidation;

namespace CoreFinance.Application.FixedAccounts.Services;

public class FixedAccountService : IFixedAccountService
{
    private readonly IFixedAccountRepository _repository;
    private readonly IValidator<CreateFixedAccountRequest> _createValidator;
    private readonly IValidator<UpdateFixedAccountRequest> _updateValidator;

    public FixedAccountService(
        IFixedAccountRepository repository,
        IValidator<CreateFixedAccountRequest> createValidator,
        IValidator<UpdateFixedAccountRequest> updateValidator)
    {
        _repository = repository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<Result<IEnumerable<FixedAccountDto>>> ObterTodosAsync()
    {
        var contas = await _repository.ObterTodosAsync();
        return Result<IEnumerable<FixedAccountDto>>.Ok(contas.Select(ToDto));
    }

    public async Task<Result<FixedAccountDto>> ObterPorIdAsync(Guid id)
    {
        var conta = await _repository.ObterPorIdAsync(id);
        if (conta is null)
            return Result<FixedAccountDto>.Fail("Conta fixa não encontrada.");

        return Result<FixedAccountDto>.Ok(ToDto(conta));
    }

    public async Task<Result<FixedAccountDto>> CriarAsync(CreateFixedAccountRequest request)
    {
        var validacao = await _createValidator.ValidateAsync(request);
        if (!validacao.IsValid)
            return Result<FixedAccountDto>.Fail(validacao.Errors[0].ErrorMessage);

        var conta = new FixedAccount(request.Name, request.Description, request.IsRequiredMonthly, request.Active);

        await _repository.AdicionarAsync(conta);
        await _repository.SalvarAsync();

        return Result<FixedAccountDto>.Ok(ToDto(conta));
    }

    public async Task<Result<FixedAccountDto>> AtualizarAsync(Guid id, UpdateFixedAccountRequest request)
    {
        var validacao = await _updateValidator.ValidateAsync(request);
        if (!validacao.IsValid)
            return Result<FixedAccountDto>.Fail(validacao.Errors[0].ErrorMessage);

        var conta = await _repository.ObterPorIdAsync(id);
        if (conta is null)
            return Result<FixedAccountDto>.Fail("Conta fixa não encontrada.");

        conta.Update(request.Name, request.Description, request.IsRequiredMonthly, request.Active);

        await _repository.AtualizarAsync(conta);
        await _repository.SalvarAsync();

        return Result<FixedAccountDto>.Ok(ToDto(conta));
    }

    public async Task<Result> RemoverAsync(Guid id)
    {
        var conta = await _repository.ObterPorIdAsync(id);
        if (conta is null)
            return Result.Fail("Conta fixa não encontrada.");

        await _repository.RemoverAsync(conta);
        await _repository.SalvarAsync();

        return Result.Ok();
    }

    private static FixedAccountDto ToDto(FixedAccount conta) => new()
    {
        Id = conta.Id,
        Name = conta.Name,
        Description = conta.Description,
        IsRequiredMonthly = conta.IsRequiredMonthly,
        Active = conta.Active,
        CreatedAt = conta.CreatedAt,
        UpdatedAt = conta.UpdatedAt
    };
}
