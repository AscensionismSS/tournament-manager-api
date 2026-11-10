using FluentValidation;

namespace Tournaments.Application.Features.Matches.RecordReplayResult;

public class RecordReplayResultValidator : AbstractValidator<RecordReplayResultCommand>
{
    public RecordReplayResultValidator()
    {
        RuleFor(x => x.IngestionDto).NotNull().WithMessage("Os dados de ingestão do replay são obrigatórios.");
        RuleFor(x => x.IngestionDto.MatchSeriesId).NotEmpty().WithMessage("O ID da série é obrigatório.");
        RuleFor(x => x.IngestionDto.WinnerTeamId).NotEmpty().WithMessage("O time vencedor é obrigatório.");
        RuleFor(x => x.IngestionDto.GameNumber).GreaterThan(0).WithMessage("O número do jogo deve ser maior que zero.");
        RuleFor(x => x.IngestionDto.DurationSeconds).GreaterThan(0).WithMessage("A duração do replay deve ser maior que zero.");
    }
}
