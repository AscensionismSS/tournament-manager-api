using FluentValidation;

namespace Tournaments.Application.Features.Draft.ConfirmSubstitution;

public class ConfirmSubstitutionValidator : AbstractValidator<ConfirmSubstitutionCommand>
{
    public ConfirmSubstitutionValidator()
    {
        RuleFor(x => x.DraftSessionId).NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");
        RuleFor(x => x.NewPlayerId).NotEmpty().WithMessage("O ID do novo jogador é obrigatório.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).WithMessage("A versão esperada deve ser maior ou igual a zero.");
    }
}
