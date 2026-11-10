using FluentValidation;

namespace Tournaments.Application.Features.Matches.RemoveLastGame;

public class RemoveLastGameValidator : AbstractValidator<RemoveLastGameCommand>
{
    public RemoveLastGameValidator()
    {
        RuleFor(x => x.MatchSeriesId).NotEmpty().WithMessage("O ID da série é obrigatório.");
    }
}
