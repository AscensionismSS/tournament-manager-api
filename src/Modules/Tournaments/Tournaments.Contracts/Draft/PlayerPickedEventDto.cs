namespace Tournaments.Contracts.Draft;

public record PlayerPickedEventDto(
    Guid DraftSessionId,
    Guid TournamentId,
    Guid TeamId,
    string TeamName,
    DraftPlayerDto Player,
    int PickIndex,
    Guid? NextTurnTeamId,
    int? NextTurnPickOrder,
    int NewDraftVersion
);
