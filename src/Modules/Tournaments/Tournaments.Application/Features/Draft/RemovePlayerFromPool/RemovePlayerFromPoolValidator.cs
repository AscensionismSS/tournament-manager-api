using FluentValidation;

namespace Tournaments.Application.Features.Draft.RemovePlayerFromPool;

public class RemovePlayerFromPoolValidator : AbstractValidator<RemovePlayerFromPoolCommand>
{
    public RemovePlayerFromPoolValidator()
    {
        RuleFor(x => x.DraftSessionId)
            .NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");

        RuleFor(x => x.PlayerId)
            .NotEmpty().WithMessage("O ID do jogador é obrigatório.");

        RuleFor(x => x.ExpectedVersion)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExpectedVersion.HasValue)
            .WithMessage("A versão esperada deve ser maior ou igual a zero.");
    }
}
