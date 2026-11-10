using FluentValidation;

namespace Identity.Application.Features.Auth.RegisterWithInvitation;

public class RegisterWithInvitationValidator : AbstractValidator<RegisterWithInvitationCommand>
{
    public RegisterWithInvitationValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("O código de convite é obrigatório.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("O nome de exibição é obrigatório.")
            .MinimumLength(2).WithMessage("O nome deve ter no mínimo 2 caracteres.")
            .MaximumLength(50).WithMessage("O nome deve ter no máximo 50 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MinimumLength(6).WithMessage("A senha deve ter no mínimo 6 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O email é obrigatório.")
            .EmailAddress().WithMessage("O email fornecido possui formato inválido.");

        RuleFor(x => x.SteamId)
            .NotNull().WithMessage("A vinculação da conta Steam é obrigatória para o cadastro.")
            .GreaterThan(76561197960265728ul).WithMessage("SteamID inválido. Faça a busca e confirme sua conta Steam.");
    }
}
