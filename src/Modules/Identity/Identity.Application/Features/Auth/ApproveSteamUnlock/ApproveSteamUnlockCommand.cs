using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.ApproveSteamUnlock;

public record ApproveSteamUnlockCommand(
    Guid RequestId
) : IRequest<Result<RecoveryActionResponseDto>>;
