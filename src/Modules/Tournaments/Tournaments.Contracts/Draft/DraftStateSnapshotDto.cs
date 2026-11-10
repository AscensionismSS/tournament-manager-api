namespace Tournaments.Contracts.Draft;

public record DraftStateSnapshotDto(
    Guid DraftSessionId,
    Guid TournamentId,
    string State,
    int TargetTeamCount,
    int CurrentPickIndex,
    Guid? CurrentTurnTeamId,
    int? CurrentTurnPickOrder,
    int DraftVersion,
    Guid? SubstitutingPlayerId,
    Guid? SubstitutingTeamId,
    IReadOnlyList<DraftPlayerDto> PlayerPool,
    IReadOnlyList<DraftTeamDto> Teams,
    int MinRequiredPlayers = 0,
    bool CanStartDraft = false,
    bool AllCaptainsAssigned = false,
    bool AllStartersPicked = false,
    int TotalStarterPicks = 0
);
