using FluentValidation;

namespace Tournaments.Application.Features.Security.UpdateSystemSecuritySettings;

public class UpdateSystemSecuritySettingsValidator : AbstractValidator<UpdateSystemSecuritySettingsCommand>
{
    public UpdateSystemSecuritySettingsValidator()
    {
        RuleFor(x => x.Message)
            .MaximumLength(1000)
            .WithMessage("A mensagem de motivo não pode ultrapassar 1000 caracteres.");
    }
}
