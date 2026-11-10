using Tournaments.Contracts.Security;
using Tournaments.Domain.Entities;

namespace Tournaments.Application.Features.Security;

public static class SystemSecuritySettingsMapper
{
    public static SystemSecuritySettingsDto ToDto(this SystemSecuritySettings settings)
    {
        return new SystemSecuritySettingsDto(
            settings.LockResultSubmission,
            settings.LockTournamentRegistration,
            settings.LockTournamentCreation,
            settings.LockDraftPicks,
            settings.MaintenanceMode,
            settings.Message,
            settings.UpdatedAtUtc,
            settings.UpdatedBy,
            settings.IsResultSubmissionLocked,
            settings.IsTournamentRegistrationLocked,
            settings.IsTournamentCreationLocked,
            settings.IsDraftPicksLocked
        );
    }
}
