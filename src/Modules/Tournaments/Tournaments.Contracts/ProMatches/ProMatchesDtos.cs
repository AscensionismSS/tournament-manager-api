namespace Tournaments.Contracts.ProMatches;

public record ProMatchesOverviewDto(
    bool IsConfigured,
    DateTime FetchedAtUtc,
    IReadOnlyList<ProMatchDto> LiveMatches,
    IReadOnlyList<ProTournamentGroupDto> Tournaments
);

public record ProTournamentGroupDto(
    long TournamentId,
    string Name,
    string LeagueName,
    string? LeagueLogoUrl,
    string? Tier,
    string? PrizePool,
    DateTime? BeginAtUtc,
    DateTime? EndAtUtc,
    bool HasLiveMatches,
    IReadOnlyList<ProMatchDto> LiveMatches,
    IReadOnlyList<ProMatchDto> UpcomingMatches
);

public record ProMatchDto(
    long MatchId,
    long TournamentId,
    string TournamentName,
    string LeagueName,
    string? LeagueLogoUrl,
    string Status,
    string Format,
    DateTime? ScheduledAtUtc,
    ProTeamDto TeamA,
    ProTeamDto TeamB,
    int ScoreTeamA,
    int ScoreTeamB,
    IReadOnlyList<ProStreamLinkDto> Streams
);

public record ProTeamDto(
    long? TeamId,
    string Name,
    string? Acronym,
    string? LogoUrl
);

public record ProStreamLinkDto(
    string Platform,
    string Language,
    string LanguageLabel,
    bool IsOfficial,
    bool IsMain,
    string Url
);
