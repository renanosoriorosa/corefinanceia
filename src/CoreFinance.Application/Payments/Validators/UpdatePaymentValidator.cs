using CoreFinance.Application.Payments.Dtos;
using FluentValidation;

namespace CoreFinance.Application.Payments.Validators;

public class UpdatePaymentValidator : AbstractValidator<UpdatePaymentRequest>
{
    public UpdatePaymentValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(255).WithMessage("Descrição deve ter no máximo 255 caracteres.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Valor deve ser maior que zero.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Mês deve ser entre 1 e 12.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Ano inválido.");

        RuleFor(x => x.FixedAccountId)
            .NotNull().WithMessage("Conta fixa é obrigatória quando IsFixedAccount é verdadeiro.")
            .When(x => x.IsFixedAccount);

        RuleFor(x => x.FixedAccountId)
            .Null().WithMessage("FixedAccountId deve ser nulo quando IsFixedAccount é falso.")
            .When(x => !x.IsFixedAccount);
    }
}
