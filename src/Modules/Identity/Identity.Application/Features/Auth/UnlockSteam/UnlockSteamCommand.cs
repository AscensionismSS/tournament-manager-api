using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.UnlockSteam;

public record UnlockSteamCommand(Guid TargetUserId) : IRequest<Result<UserDto>>;
