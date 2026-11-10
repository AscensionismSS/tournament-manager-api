using FluentValidation;

namespace Tournaments.Application.Features.Tournaments.StartTournament;

public class StartTournamentValidator : AbstractValidator<StartTournamentCommand>
{
    public StartTournamentValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty().WithMessage("O ID do torneio é obrigatório.");
    }
}
