using FluentValidation;

namespace Tournaments.Application.Features.Matches.RecordManualResult;

public class RecordManualResultValidator : AbstractValidator<RecordManualResultCommand>
{
    public RecordManualResultValidator()
    {
        RuleFor(x => x.MatchSeriesId)
            .NotEmpty().WithMessage("O ID da série é obrigatório.");

        RuleFor(x => x.WinnerTeamId)
            .NotEmpty().WithMessage("O time vencedor é obrigatório.");

        RuleFor(x => x.DurationSeconds)
            .GreaterThan(0).WithMessage("A duração da partida deve ser maior que zero.");

        RuleFor(x => x.TeamAKills)
            .GreaterThanOrEqualTo(0).WithMessage("O número de abates da equipe A deve ser maior ou igual a zero.");

        RuleFor(x => x.TeamBKills)
            .GreaterThanOrEqualTo(0).WithMessage("O número de abates da equipe B deve ser maior ou igual a zero.");
    }
}
