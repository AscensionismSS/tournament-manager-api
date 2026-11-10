namespace Tournaments.Contracts.Draft;

public record DraftCompletedEventDto(
    Guid DraftSessionId,
    Guid TournamentId,
    int NewDraftVersion
);
