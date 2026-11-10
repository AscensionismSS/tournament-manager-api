namespace Tournaments.Contracts.Draft;

public record DraftPlayerDto(
    Guid Id,
    string Name,
    string? ExternalUserId,
    IReadOnlyList<string>? Roles = null,
    string? PreferredRole = null,
    int? RankTier = null,
    int? LeaderboardRank = null,
    string? AvatarUrl = null,
    string? DotaFriendId = null,
    string? SteamProfileUrl = null
);
