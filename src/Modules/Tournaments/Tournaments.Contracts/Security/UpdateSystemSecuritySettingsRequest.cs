namespace Tournaments.Contracts.Security;

public record UpdateSystemSecuritySettingsRequest(
    bool? LockResultSubmission = null,
    bool? LockTournamentRegistration = null,
    bool? LockTournamentCreation = null,
    bool? LockDraftPicks = null,
    bool? MaintenanceMode = null,
    string? Message = null
);
