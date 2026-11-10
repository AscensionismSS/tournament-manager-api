using FluentValidation;

namespace Tournaments.Application.Features.Matches.RollbackMatchSeries;

public class RollbackMatchSeriesValidator : AbstractValidator<RollbackMatchSeriesCommand>
{
    public RollbackMatchSeriesValidator()
    {
        RuleFor(x => x.MatchSeriesId).NotEmpty().WithMessage("O ID da série é obrigatório.");
    }
}
