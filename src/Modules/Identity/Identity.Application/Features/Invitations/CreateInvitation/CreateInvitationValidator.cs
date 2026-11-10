using FluentValidation;

namespace Identity.Application.Features.Invitations.CreateInvitation;

public class CreateInvitationValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O email é obrigatório.")
            .MaximumLength(254).WithMessage("O email deve conter no máximo 254 caracteres.")
            .EmailAddress().WithMessage("O formato do email é inválido.");

        RuleFor(x => x.TargetRole)
            .IsInEnum().WithMessage("Role inválida.");

        RuleFor(x => x.ValidHours)
            .Must(hours => Identity.Domain.Entities.Invitation.AllowedExpirationHours.Contains(hours))
            .WithMessage("A validade do convite deve ser de 24, 48 ou 72 horas.");
    }
}
