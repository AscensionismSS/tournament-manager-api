namespace Identity.Contracts.Steam;

public record SteamPreviewDto(
    ulong SteamId,
    string PersonaName,
    string? AvatarUrl,
    int? RankTier,
    int? LeaderboardRank,
    bool IsAlreadyInUse,
    string? AlreadyInUseMessage,
    string? SteamIdString = null,
    string? ProfileUrl = null
);
