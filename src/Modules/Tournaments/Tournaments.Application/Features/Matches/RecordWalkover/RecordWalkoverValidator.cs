using FluentValidation;

namespace Tournaments.Application.Features.Matches.RecordWalkover;

public class RecordWalkoverValidator : AbstractValidator<RecordWalkoverCommand>
{
    public RecordWalkoverValidator()
    {
        RuleFor(x => x.MatchSeriesId).NotEmpty().WithMessage("O ID da série é obrigatório.");
        RuleFor(x => x.WinnerTeamId).NotEmpty().WithMessage("O time vencedor do W.O. é obrigatório.");
    }
}
