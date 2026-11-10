using Tournaments.Contracts.Replay;

namespace Tournaments.Contracts.Matches;

public record MatchGameDto(
    Guid Id,
    int GameNumber,
    string ResultSource,
    Guid WinnerTeamId,
    long DurationSeconds,
    int TeamAKills,
    int TeamBKills,
    bool IsWalkover,
    long? DotaMatchId,
    Guid? FirstPickTeamId = null,
    IReadOnlyList<int>? TeamABanHeroIds = null,
    IReadOnlyList<int>? TeamBBanHeroIds = null,
    IReadOnlyList<PlayerMatchStatsDto>? PlayerStats = null,
    int TeamARoshanKills = 0,
    int TeamBRoshanKills = 0,
    int TeamAAegisStolen = 0,
    int TeamBAegisStolen = 0
);
