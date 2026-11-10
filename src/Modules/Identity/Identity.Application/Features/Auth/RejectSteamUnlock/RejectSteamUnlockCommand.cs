using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.RejectSteamUnlock;

public record RejectSteamUnlockCommand(
    Guid RequestId,
    string? Reason = null
) : IRequest<Result<RecoveryActionResponseDto>>;
