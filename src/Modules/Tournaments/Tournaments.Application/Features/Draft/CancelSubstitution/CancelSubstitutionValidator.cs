using FluentValidation;

namespace Tournaments.Application.Features.Draft.CancelSubstitution;

public class CancelSubstitutionValidator : AbstractValidator<CancelSubstitutionCommand>
{
    public CancelSubstitutionValidator()
    {
        RuleFor(x => x.DraftSessionId).NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).WithMessage("A versão esperada deve ser maior ou igual a zero.");
    }
}
