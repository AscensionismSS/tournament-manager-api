namespace Tournaments.Contracts.Security;

public record SystemSecuritySettingsDto(
    bool LockResultSubmission,
    bool LockTournamentRegistration,
    bool LockTournamentCreation,
    bool LockDraftPicks,
    bool MaintenanceMode,
    string? Message,
    DateTime? UpdatedAtUtc,
    string? UpdatedBy,
    bool IsResultSubmissionLocked,
    bool IsTournamentRegistrationLocked,
    bool IsTournamentCreationLocked,
    bool IsDraftPicksLocked
);
