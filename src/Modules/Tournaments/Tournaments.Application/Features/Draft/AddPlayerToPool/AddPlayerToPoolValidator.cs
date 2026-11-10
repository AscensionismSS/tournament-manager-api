using FluentValidation;

namespace Tournaments.Application.Features.Draft.AddPlayerToPool;

public class AddPlayerToPoolValidator : AbstractValidator<AddPlayerToPoolCommand>
{
    public AddPlayerToPoolValidator()
    {
        RuleFor(x => x.DraftSessionId).NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");
        RuleFor(x => x.PlayerId).NotEmpty().WithMessage("O ID do jogador é obrigatório.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do jogador é obrigatório.")
            .MaximumLength(100).WithMessage("O nome do jogador pode ter no máximo 100 caracteres.");
    }
}
