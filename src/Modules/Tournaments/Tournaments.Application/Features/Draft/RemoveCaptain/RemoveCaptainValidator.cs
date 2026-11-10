using FluentValidation;

namespace Tournaments.Application.Features.Draft.RemoveCaptain;

public class RemoveCaptainValidator : AbstractValidator<RemoveCaptainCommand>
{
    public RemoveCaptainValidator()
    {
        RuleFor(x => x.DraftSessionId).NotEmpty().WithMessage("O ID da sessão de draft é obrigatório.");
        RuleFor(x => x.TeamId).NotEmpty().WithMessage("O ID da equipe é obrigatório.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).WithMessage("A versão esperada deve ser maior ou igual a zero.");
    }
}
