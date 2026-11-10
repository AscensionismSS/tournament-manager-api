using FluentValidation;

namespace Tournaments.Application.Features.Draft.CompleteDraft;

public class CompleteDraftValidator : AbstractValidator<CompleteDraftCommand>
{
    public CompleteDraftValidator()
    {
        RuleFor(x => x.DraftSessionId).NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).WithMessage("A versão esperada deve ser maior ou igual a zero.");
    }
}
