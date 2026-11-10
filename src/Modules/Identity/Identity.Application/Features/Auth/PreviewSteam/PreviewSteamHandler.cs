using System.Text.RegularExpressions;
using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Steam;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.PreviewSteam;

public class PreviewSteamHandler : IRequestHandler<PreviewSteamQuery, Result<SteamPreviewDto>>
{
    private const ulong SteamIdOffset = 76561197960265728ul;
    private readonly IUserRepository _userRepository;
    private readonly IOpenDotaProfileService _openDotaProfileService;
    private readonly ICurrentUserService _currentUserService;

    public PreviewSteamHandler(
        IUserRepository userRepository,
        IOpenDotaProfileService openDotaProfileService,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _openDotaProfileService = openDotaProfileService;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SteamPreviewDto>> Handle(PreviewSteamQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdentifierOrUrl))
        {
            return Result.Failure<SteamPreviewDto>("Informe o SteamID ou o link do perfil da Steam.");
        }

        ulong steamId = await _openDotaProfileService.ResolveSteamIdAsync(request.IdentifierOrUrl.Trim(), cancellationToken);
        if (steamId < SteamIdOffset)
        {
            return Result.Failure<SteamPreviewDto>("SteamID ou Perfil Steam não encontrado ou inválido. Use o link do seu perfil (ex: https://steamcommunity.com/id/seu_nick ou https://steamcommunity.com/profiles/76561198000000001) ou seu SteamID numérico.");
        }

        // Verifica se já está em uso por outro usuário
        var existingUser = await _userRepository.GetBySteamIdAsync(steamId, cancellationToken);
        bool isAlreadyInUse = existingUser is not null && existingUser.Id != _currentUserService.UserId;
        string? inUseMessage = isAlreadyInUse
            ? $"Esta conta Steam já está vinculada ao usuário '{existingUser?.DisplayName}'."
            : null;

        var profile = await _openDotaProfileService.FetchProfileAsync(steamId, cancellationToken);

        string personaName = profile?.PersonaName ?? $"Player_{steamId - SteamIdOffset}";
        string? avatarUrl = profile?.AvatarUrl;
        int? rankTier = profile?.RankTier;
        int? leaderboardRank = profile?.LeaderboardRank;

        var dto = new SteamPreviewDto(
            steamId,
            personaName,
            avatarUrl,
            rankTier,
            leaderboardRank,
            isAlreadyInUse,
            inUseMessage,
            SteamIdString: steamId.ToString(),
            ProfileUrl: $"https://steamcommunity.com/profiles/{steamId}"
        );

        return Result.Success(dto);
    }
}
