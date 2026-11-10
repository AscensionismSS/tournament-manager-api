using FluentValidation;

namespace Tournaments.Application.Features.Draft.RenameTeam;

public class RenameTeamValidator : AbstractValidator<RenameTeamCommand>
{
    public RenameTeamValidator()
    {
        RuleFor(x => x.DraftSessionId).NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");
        RuleFor(x => x.TeamId).NotEmpty().WithMessage("O ID da equipe é obrigatório.");
        RuleFor(x => x.NewName)
            .Must(name => string.IsNullOrWhiteSpace(name) || (name.Trim().Length >= 2 && name.Trim().Length <= 100))
            .WithMessage("O nome da equipe deve ter entre 2 e 100 caracteres quando informado.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).WithMessage("A versão esperada deve ser maior ou igual a zero.");
    }
}
