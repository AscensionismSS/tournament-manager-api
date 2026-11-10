using Identity.Contracts.Preferences;

namespace Identity.Contracts.Auth;

public record AuthResponseDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    ulong? SteamId,
    bool IsSteamLocked,
    int? DotaRankTier,
    int? DotaLeaderboardRank,
    string? DotaPersonaName,
    string? DotaAvatarUrl,
    string Token,
    DateTime ExpiresAtUtc,
    IReadOnlyList<string>? Roles = null,
    string? PreferredRole = null,
    string? SteamProfileUrl = null,
    GeneralPreferencesDto? GeneralPreferences = null
)
{
    public bool IsSteamLinked => SteamId.HasValue && SteamId.Value > 0;
    public string PreferredLandingPage => GeneralPreferences?.PreferredLandingPage ?? "Player";
}

public record UserDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    ulong? SteamId,
    bool IsSteamLocked,
    int? DotaRankTier,
    int? DotaLeaderboardRank,
    string? DotaPersonaName,
    string? DotaAvatarUrl,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<string>? Roles = null,
    string? PreferredRole = null,
    string? SteamProfileUrl = null,
    GeneralPreferencesDto? GeneralPreferences = null
)
{
    public bool IsSteamLinked => SteamId.HasValue && SteamId.Value > 0;
    public string PreferredLandingPage => GeneralPreferences?.PreferredLandingPage ?? "Player";
}

