using FluentValidation;

namespace Identity.Application.Features.Auth.Login;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage("O login ou email é obrigatório.")
            .MaximumLength(254).WithMessage("O login ou email deve conter no máximo 254 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MinimumLength(6).WithMessage("A senha deve ter no mínimo 6 caracteres.");
    }
}
