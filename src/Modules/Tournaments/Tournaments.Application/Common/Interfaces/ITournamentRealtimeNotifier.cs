using Tournaments.Contracts.Draft;
using Tournaments.Contracts.Security;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Common.Interfaces;

public interface ITournamentRealtimeNotifier
{
    Task NotifyDraftStateSnapshotAsync(Guid tournamentId, DraftStateSnapshotDto snapshot, CancellationToken ct = default);
    Task NotifyPlayerPickedAsync(Guid tournamentId, PlayerPickedEventDto evt, CancellationToken ct = default);
    Task NotifySubstitutionStartedAsync(Guid tournamentId, SubstitutionStartedEventDto evt, CancellationToken ct = default);
    Task NotifySubstitutionConfirmedAsync(Guid tournamentId, SubstitutionConfirmedEventDto evt, CancellationToken ct = default);
    Task NotifySubstitutionCanceledAsync(Guid tournamentId, SubstitutionCanceledEventDto evt, CancellationToken ct = default);
    Task NotifyDraftCompletedAsync(Guid tournamentId, DraftCompletedEventDto evt, CancellationToken ct = default);
    Task NotifyTournamentWebhookAsync(TournamentWebhookEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
    Task NotifySecuritySettingsChangedAsync(SystemSecuritySettingsDto settings, CancellationToken ct = default) => Task.CompletedTask;
}
