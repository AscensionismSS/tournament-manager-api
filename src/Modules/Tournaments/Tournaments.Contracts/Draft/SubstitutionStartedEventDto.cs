namespace Tournaments.Contracts.Draft;

public record SubstitutionStartedEventDto(
    Guid DraftSessionId,
    Guid TournamentId,
    Guid TeamId,
    Guid PlayerId,
    int NewDraftVersion
);
