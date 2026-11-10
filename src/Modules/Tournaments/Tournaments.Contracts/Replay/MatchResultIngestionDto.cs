namespace Tournaments.Contracts.Replay;

public record MatchResultIngestionDto(
    Guid MatchSeriesId,
    int GameNumber,
    Guid WinnerTeamId,
    long DurationSeconds,
    int TeamAKills,
    int TeamBKills,
    long? DotaMatchId,
    IReadOnlyList<PlayerMatchStatsDto>? PlayerStats,
    Guid? FirstPickTeamId = null,
    IReadOnlyList<int>? TeamABanHeroIds = null,
    IReadOnlyList<int>? TeamBBanHeroIds = null,
    int TeamARoshanKills = 0,
    int TeamBRoshanKills = 0,
    int TeamAAegisStolen = 0,
    int TeamBAegisStolen = 0
);
