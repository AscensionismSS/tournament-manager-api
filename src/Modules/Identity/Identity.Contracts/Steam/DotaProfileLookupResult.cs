namespace Identity.Contracts.Steam;

public record DotaProfileLookupResult(
    ulong SteamId,
    string PersonaName,
    string? AvatarUrl,
    int? RankTier,
    int? LeaderboardRank
);
