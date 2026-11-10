using FluentValidation;

namespace Tournaments.Application.Features.Tournaments.AddTeamToTournament;

public class AddTeamToTournamentValidator : AbstractValidator<AddTeamToTournamentCommand>
{
    public AddTeamToTournamentValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty().WithMessage("O ID do torneio é obrigatório.");
        RuleFor(x => x.TeamId).NotEmpty().WithMessage("O ID da equipe é obrigatório.");
        RuleFor(x => x.TeamName)
            .NotEmpty().WithMessage("O nome da equipe é obrigatório.")
            .MaximumLength(150).WithMessage("O nome da equipe pode ter no máximo 150 caracteres.");
    }
}
