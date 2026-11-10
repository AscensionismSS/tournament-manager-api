using BuildingBlocks.Domain;
using Identity.Contracts.Auth;
using MediatR;

namespace Identity.Application.Features.Auth.LinkSteam;

public record LinkSteamCommand(ulong SteamId) : IRequest<Result<UserDto>>;
