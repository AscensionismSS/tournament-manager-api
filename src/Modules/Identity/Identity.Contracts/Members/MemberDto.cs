namespace Identity.Contracts.Members;

/// <summary>
/// Contrato definitivo (Context-Specific DTO) para listagem e exibição de cards de membros.
/// Enxuto, sem aliases redundantes e focado exclusivamente no contexto de exibição competitiva.
/// </summary>
public record MemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string? PersonaName,
    string? AvatarUrl,
    string? SteamId,
    string? SteamProfileUrl,
    string? DotaFriendId,
    int? RankTier,
    int? LeaderboardRank,
    IReadOnlyList<string> Roles,
    string? PreferredRole,
    string Role,
    bool IsSteamLocked,
    bool IsActive = true,
    string? StatusReason = null,
    bool IsTournamentEligible = true
);
