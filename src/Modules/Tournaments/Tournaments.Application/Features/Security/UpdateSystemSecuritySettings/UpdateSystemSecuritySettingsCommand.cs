using MediatR;
using Tournaments.Contracts.Security;

namespace Tournaments.Application.Features.Security.UpdateSystemSecuritySettings;

public record UpdateSystemSecuritySettingsCommand(
    bool? LockResultSubmission,
    bool? LockTournamentRegistration,
    bool? LockTournamentCreation,
    bool? LockDraftPicks,
    bool? MaintenanceMode,
    string? Message,
    string UpdatedBy
) : IRequest<SystemSecuritySettingsDto>;
