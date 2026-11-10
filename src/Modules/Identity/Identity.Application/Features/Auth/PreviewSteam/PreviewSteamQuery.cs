using BuildingBlocks.Domain;
using Identity.Contracts.Steam;
using MediatR;

namespace Identity.Application.Features.Auth.PreviewSteam;

public record PreviewSteamQuery(string IdentifierOrUrl) : IRequest<Result<SteamPreviewDto>>;
