using BuildingBlocks.Domain;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Identity.Domain.Repositories;
using MediatR;

namespace Identity.Application.Features.Auth.LinkSteam;

public class LinkSteamHandler : IRequestHandler<LinkSteamCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOpenDotaProfileService _openDotaProfileService;

    public LinkSteamHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IOpenDotaProfileService openDotaProfileService)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _openDotaProfileService = openDotaProfileService;
    }

    public async Task<Result<UserDto>> Handle(LinkSteamCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Result.Failure<UserDto>("Usuário não autenticado.");
        }

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserDto>("Usuário não encontrado.");
        }

        if (user.IsSteamLocked)
        {
            return Result.Failure<UserDto>("Sua conta Steam já está vinculada e bloqueada. Solicite a um administrador para alterá-la.");
        }

        // Verifica se outro usuário já vinculou essa mesma Steam
        var existingSteamUser = await _userRepository.GetBySteamIdAsync(request.SteamId, cancellationToken);
        if (existingSteamUser is not null && existingSteamUser.Id != user.Id)
        {
            return Result.Failure<UserDto>("Esta conta Steam já está vinculada a outro usuário na plataforma.");
        }

        // Consulta dados de rank e perfil via OpenDota
        var profile = await _openDotaProfileService.FetchProfileAsync(request.SteamId, cancellationToken);

        string personaName = profile?.PersonaName ?? $"Player_{request.SteamId}";
        string? avatarUrl = profile?.AvatarUrl;
        int? rankTier = profile?.RankTier;
        int? leaderboardRank = profile?.LeaderboardRank;

        user.LinkSteamAccount(request.SteamId, personaName, avatarUrl, rankTier, leaderboardRank);

        await _userRepository.UpdateAsync(user, cancellationToken);

        return Result.Success(user.ToDto());
    }
}
