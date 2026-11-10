using FluentValidation;

namespace Tournaments.Application.Features.Draft.FinalizeCaptainOrder;

public class FinalizeCaptainOrderValidator : AbstractValidator<FinalizeCaptainOrderCommand>
{
    public FinalizeCaptainOrderValidator()
    {
        RuleFor(x => x.DraftSessionId).NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).WithMessage("A versão esperada deve ser maior ou igual a zero.");
    }
}
