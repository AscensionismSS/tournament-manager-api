using FluentValidation;

namespace Tournaments.Application.Features.Tournaments.GetTournamentFormatOptions;

public class GetTournamentFormatOptionsValidator : AbstractValidator<GetTournamentFormatOptionsQuery>
{
    public GetTournamentFormatOptionsValidator()
    {
        RuleFor(x => x.TeamCount)
            .InclusiveBetween(4, 8)
            .When(x => x.TeamCount.HasValue)
            .WithMessage("O motor de torneios suporta de 4 a 8 equipes.");
    }
}
