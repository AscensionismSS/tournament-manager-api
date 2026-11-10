using FluentValidation;

namespace Tournaments.Application.Features.Draft.StartSubstitution;

public class StartSubstitutionValidator : AbstractValidator<StartSubstitutionCommand>
{
    public StartSubstitutionValidator()
    {
        RuleFor(x => x.DraftSessionId).NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");
        RuleFor(x => x.PlayerId).NotEmpty().WithMessage("O ID do jogador é obrigatório.");
        RuleFor(x => x.TeamId).NotEmpty().WithMessage("O ID da equipe é obrigatório.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).WithMessage("A versão esperada deve ser maior ou igual a zero.");
    }
}
