using FluentValidation;

namespace Tournaments.Application.Features.Matches.UpdateGameWalkover;

public class UpdateGameWalkoverValidator : AbstractValidator<UpdateGameWalkoverCommand>
{
    public UpdateGameWalkoverValidator()
    {
        RuleFor(x => x.MatchSeriesId).NotEmpty().WithMessage("O ID da série é obrigatório.");
        RuleFor(x => x.GameNumber).GreaterThan(0).WithMessage("O número do jogo deve ser maior que zero.");
        RuleFor(x => x.WinnerTeamId).NotEmpty().WithMessage("O time vencedor do W.O. é obrigatório.");
    }
}
