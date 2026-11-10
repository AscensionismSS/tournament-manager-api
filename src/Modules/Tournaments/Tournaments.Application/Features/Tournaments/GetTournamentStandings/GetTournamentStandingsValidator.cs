using FluentValidation;

namespace Tournaments.Application.Features.Tournaments.GetTournamentStandings;

public class GetTournamentStandingsValidator : AbstractValidator<GetTournamentStandingsQuery>
{
    public GetTournamentStandingsValidator()
    {
        RuleFor(x => x.TournamentId)
            .NotEmpty().WithMessage("O ID do torneio é obrigatório.");
    }
}
