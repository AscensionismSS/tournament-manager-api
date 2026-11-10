using Identity.Contracts.Auth;
using Identity.Contracts.Preferences;
using Identity.Domain.Entities;

namespace Identity.Application.Features.Auth;

public static class UserMapper
{
    public static UserDto ToDto(this User user)
    {
        return new UserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.SteamId,
            user.IsSteamLocked,
            user.DotaRankTier,
            user.DotaLeaderboardRank,
            user.DotaPersonaName,
            user.DotaAvatarUrl,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            user.Roles.Select(r => r.ToString()).ToList(),
            user.PreferredRole?.ToString(),
            user.SteamId.HasValue ? $"https://steamcommunity.com/profiles/{user.SteamId.Value}" : null,
            new GeneralPreferencesDto(user.Preferences?.PreferredLandingPage ?? "Player")
        );
    }

    public static AuthResponseDto ToAuthResponseDto(this User user, string token, DateTime expiresAtUtc)
    {
        return new AuthResponseDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.SteamId,
            user.IsSteamLocked,
            user.DotaRankTier,
            user.DotaLeaderboardRank,
            user.DotaPersonaName,
            user.DotaAvatarUrl,
            token,
            expiresAtUtc,
            user.Roles.Select(r => r.ToString()).ToList(),
            user.PreferredRole?.ToString(),
            user.SteamId.HasValue ? $"https://steamcommunity.com/profiles/{user.SteamId.Value}" : null,
            new GeneralPreferencesDto(user.Preferences?.PreferredLandingPage ?? "Player")
        );
    }
}
