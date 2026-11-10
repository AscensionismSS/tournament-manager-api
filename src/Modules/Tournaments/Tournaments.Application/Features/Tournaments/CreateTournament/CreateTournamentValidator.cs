using FluentValidation;

namespace Tournaments.Application.Features.Tournaments.CreateTournament;

public class CreateTournamentValidator : AbstractValidator<CreateTournamentCommand>
{
    public CreateTournamentValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do torneio é obrigatório.")
            .MaximumLength(150).WithMessage("O nome do torneio pode ter no máximo 150 caracteres.");

        RuleFor(x => x.TeamCount)
            .InclusiveBetween(4, 8).WithMessage("A quantidade de equipes deve ser entre 4 e 8.");

        RuleFor(x => x.GroupCount)
            .InclusiveBetween(1, 2).WithMessage("A quantidade de grupos permitida é 1 ou 2.");
    }
}
