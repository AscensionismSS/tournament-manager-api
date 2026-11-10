namespace Tournaments.Contracts.Draft;

public record SubstitutionConfirmedEventDto(
    Guid DraftSessionId,
    Guid TournamentId,
    Guid TeamId,
    DraftPlayerDto OldPlayer,
    DraftPlayerDto NewPlayer,
    int NewDraftVersion
);
