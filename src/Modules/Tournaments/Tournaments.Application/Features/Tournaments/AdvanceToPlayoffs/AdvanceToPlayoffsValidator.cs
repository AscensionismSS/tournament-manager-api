using FluentValidation;

namespace Tournaments.Application.Features.Tournaments.AdvanceToPlayoffs;

public class AdvanceToPlayoffsValidator : AbstractValidator<AdvanceToPlayoffsCommand>
{
    public AdvanceToPlayoffsValidator()
    {
        RuleFor(x => x.TournamentId)
            .NotEmpty().WithMessage("O ID do torneio é obrigatório.");
    }
}
