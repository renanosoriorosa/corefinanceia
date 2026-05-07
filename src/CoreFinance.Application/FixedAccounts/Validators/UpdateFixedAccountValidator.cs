using CoreFinance.Application.FixedAccounts.Dtos;
using FluentValidation;

namespace CoreFinance.Application.FixedAccounts.Validators;

public class UpdateFixedAccountValidator : AbstractValidator<UpdateFixedAccountRequest>
{
    public UpdateFixedAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(100).WithMessage("Nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Descrição deve ter no máximo 255 caracteres.")
            .When(x => x.Description is not null);
    }
}
