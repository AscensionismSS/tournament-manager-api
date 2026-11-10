using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;

namespace Tournaments.UnitTests;

// Mock Notifier para testes da camada Application
public class TestRealtimeNotifier : ITournamentRealtimeNotifier
{
    public List<string> Notifications { get; } = [];

    public Task NotifyDraftStateSnapshotAsync(Guid tournamentId, DraftStateSnapshotDto snapshot, CancellationToken ct = default)
    {
        Notifications.Add($"Snapshot_v{snapshot.DraftVersion}");
        return Task.CompletedTask;
    }

    public Task NotifyPlayerPickedAsync(Guid tournamentId, PlayerPickedEventDto evt, CancellationToken ct = default)
    {
        Notifications.Add($"PlayerPicked_{evt.Player.Name}");
        return Task.CompletedTask;
    }

    public Task NotifySubstitutionStartedAsync(Guid tournamentId, SubstitutionStartedEventDto evt, CancellationToken ct = default)
    {
        Notifications.Add($"SubstitutionStarted_{evt.PlayerId}");
        return Task.CompletedTask;
    }

    public Task NotifySubstitutionConfirmedAsync(Guid tournamentId, SubstitutionConfirmedEventDto evt, CancellationToken ct = default)
    {
        Notifications.Add("SubstitutionConfirmed");
        return Task.CompletedTask;
    }

    public Task NotifySubstitutionCanceledAsync(Guid tournamentId, SubstitutionCanceledEventDto evt, CancellationToken ct = default)
    {
        Notifications.Add("SubstitutionCanceled");
        return Task.CompletedTask;
    }

    public Task NotifyDraftCompletedAsync(Guid tournamentId, DraftCompletedEventDto evt, CancellationToken ct = default)
    {
        Notifications.Add("DraftCompleted");
        return Task.CompletedTask;
    }
}
