namespace Tournaments.Contracts.Tournaments;

public record TournamentWebhookEventDto(
    Guid TournamentId,
    string TournamentName,
    string EventType,
    string Message,
    int Version,
    string Status,
    Guid? MatchSeriesId,
    string? SlotCode,
    string? WinnerTeamName,
    DateTime TimestampUtc
);
