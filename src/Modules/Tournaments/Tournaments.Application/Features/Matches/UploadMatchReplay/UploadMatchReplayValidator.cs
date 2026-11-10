using FluentValidation;

namespace Tournaments.Application.Features.Matches.UploadMatchReplay;

public class UploadMatchReplayValidator : AbstractValidator<UploadMatchReplayCommand>
{
    public UploadMatchReplayValidator()
    {
        RuleFor(x => x.MatchSeriesId)
            .NotEmpty()
            .WithMessage("O ID da série da partida é obrigatório.");

        RuleFor(x => x.ReplayStream)
            .NotNull()
            .WithMessage("O arquivo de replay é obrigatório.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("O nome do arquivo de replay é obrigatório.")
            .Must(f => f.EndsWith(".dem", StringComparison.OrdinalIgnoreCase))
            .WithMessage("O arquivo de replay deve possuir extensão '.dem'.");

        RuleFor(x => x.GameNumber)
            .GreaterThan(0)
            .When(x => x.GameNumber.HasValue)
            .WithMessage("O número do jogo deve ser maior que zero.");
    }
}
