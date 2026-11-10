using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.RegisterWithInvitation;

public record RegisterWithInvitationCommand(
    string Code,
    string Password,
    string DisplayName,
    string? Email = null,
    ulong? SteamId = null
) : IRequest<Result<AuthResponseDto>>;
