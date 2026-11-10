namespace Tournaments.Contracts.Tournaments;

public record TeamStandingDto(
    int Rank,
    Guid TeamId,
    string TeamName,
    int Points,
    int PlayedMatches,
    int WonMatches,
    int DrawnMatches,
    int LostMatches,
    int MapWins,
    int MapLosses,
    int MapDifferential,
    long TotalVictoryDurationSeconds,
    int TotalKills,
    int TotalDeaths,
    int KillDifferential,
    string? GroupCode = null
);

public record TournamentStandingsDto(
    Guid TournamentId,
    string TournamentName,
    string Status,
    int Version,
    DateTime LastUpdatedAtUtc,
    IReadOnlyList<TeamStandingDto> Standings
);
