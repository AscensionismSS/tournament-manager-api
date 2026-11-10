using FluentValidation;

namespace Identity.Application.Features.Auth.LinkSteam;

public class LinkSteamValidator : AbstractValidator<LinkSteamCommand>
{
    public LinkSteamValidator()
    {
        RuleFor(x => x.SteamId)
            .GreaterThan(0ul).WithMessage("SteamID64 inválido.")
            .Must(id => id > 76561197960265728ul).WithMessage("SteamID64 deve ser um identificador válido de 64 bits (ex: 7656119xxxxxxxxxx).");
    }
}
