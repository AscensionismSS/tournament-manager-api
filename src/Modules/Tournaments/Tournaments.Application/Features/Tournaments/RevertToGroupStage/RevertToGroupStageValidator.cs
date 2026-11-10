using FluentValidation;

namespace Tournaments.Application.Features.Tournaments.RevertToGroupStage;

public class RevertToGroupStageValidator : AbstractValidator<RevertToGroupStageCommand>
{
    public RevertToGroupStageValidator()
    {
        RuleFor(x => x.TournamentId)
            .NotEmpty().WithMessage("O ID do torneio é obrigatório.");
    }
}
