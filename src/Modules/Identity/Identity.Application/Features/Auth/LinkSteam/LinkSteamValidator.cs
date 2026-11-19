using FluentValidation;
using Identity.Domain.Entities;

namespace Identity.Application.Features.Auth.LinkSteam;

public class LinkSteamValidator : AbstractValidator<LinkSteamCommand>
{
    public LinkSteamValidator()
    {
        RuleFor(x => x.SteamId)
            .GreaterThan(0ul).WithMessage("SteamID64 inválido.")
            .Must(User.IsValidSteamId64).WithMessage("SteamID64 deve ser um identificador válido de 64 bits (ex: 7656119xxxxxxxxxx).");
    }
}
