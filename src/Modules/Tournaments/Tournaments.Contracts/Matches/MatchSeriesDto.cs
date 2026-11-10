namespace Tournaments.Contracts.Matches;

public record MatchSeriesDto(
    Guid Id,
    Guid TournamentId,
    string SlotCode,
    string Format,
    string Status,
    Guid? TeamAId,
    string? TeamAName,
    int ScoreTeamA,
    Guid? TeamBId,
    string? TeamBName,
    int ScoreTeamB,
    Guid? WinnerTeamId,
    Guid? LoserTeamId,
    IReadOnlyList<MatchGameDto> Games,
    int Round = 1
);
