using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.RequestSteamUnlock;

public record RequestSteamUnlockCommand(string? Reason = null) : IRequest<Result<RecoveryActionResponseDto>>;
