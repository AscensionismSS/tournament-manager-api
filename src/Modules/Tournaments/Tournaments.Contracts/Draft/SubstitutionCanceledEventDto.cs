namespace Tournaments.Contracts.Draft;

public record SubstitutionCanceledEventDto(
    Guid DraftSessionId,
    Guid TournamentId,
    int NewDraftVersion
);
