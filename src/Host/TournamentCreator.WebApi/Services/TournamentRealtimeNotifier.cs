using Microsoft.AspNetCore.SignalR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Contracts.Tournaments;
using TournamentCreator.WebApi.Hubs.v1;

namespace TournamentCreator.WebApi.Services;

public class TournamentRealtimeNotifier : ITournamentRealtimeNotifier
{
    private readonly IHubContext<TournamentDraftHub> _hubContext;
    private readonly TournamentWebhookStreamService _webhookStream;
    private readonly ILogger<TournamentRealtimeNotifier> _logger;

    public TournamentRealtimeNotifier(
        IHubContext<TournamentDraftHub> hubContext,
        TournamentWebhookStreamService webhookStream,
        ILogger<TournamentRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _webhookStream = webhookStream;
        _logger = logger;
    }

    public async Task NotifyDraftStateSnapshotAsync(Guid tournamentId, DraftStateSnapshotDto snapshot, CancellationToken ct = default)
    {
        string group = TournamentDraftHub.GetGroupName(tournamentId);
        _logger.LogInformation("Enviando snapshot de estado do draft v{Version} para o grupo {Group}", snapshot.DraftVersion, group);
        await _hubContext.Clients.Group(group).SendAsync("ReceiveDraftStateSnapshot", snapshot, cancellationToken: ct);
    }

    public async Task NotifyPlayerPickedAsync(Guid tournamentId, PlayerPickedEventDto evt, CancellationToken ct = default)
    {
        string group = TournamentDraftHub.GetGroupName(tournamentId);
        _logger.LogInformation("Notificando pick do jogador {PlayerName} para time {TeamName} no grupo {Group}", evt.Player.Name, evt.TeamName, group);
        await _hubContext.Clients.Group(group).SendAsync("ReceivePlayerPicked", evt, cancellationToken: ct);
    }

    public async Task NotifySubstitutionStartedAsync(Guid tournamentId, SubstitutionStartedEventDto evt, CancellationToken ct = default)
    {
        string group = TournamentDraftHub.GetGroupName(tournamentId);
        await _hubContext.Clients.Group(group).SendAsync("ReceiveSubstitutionStarted", evt, cancellationToken: ct);
    }

    public async Task NotifySubstitutionConfirmedAsync(Guid tournamentId, SubstitutionConfirmedEventDto evt, CancellationToken ct = default)
    {
        string group = TournamentDraftHub.GetGroupName(tournamentId);
        await _hubContext.Clients.Group(group).SendAsync("ReceiveSubstitutionConfirmed", evt, cancellationToken: ct);
    }

    public async Task NotifySubstitutionCanceledAsync(Guid tournamentId, SubstitutionCanceledEventDto evt, CancellationToken ct = default)
    {
        string group = TournamentDraftHub.GetGroupName(tournamentId);
        await _hubContext.Clients.Group(group).SendAsync("ReceiveSubstitutionCanceled", evt, cancellationToken: ct);
    }

    public async Task NotifyDraftCompletedAsync(Guid tournamentId, DraftCompletedEventDto evt, CancellationToken ct = default)
    {
        string group = TournamentDraftHub.GetGroupName(tournamentId);
        await _hubContext.Clients.Group(group).SendAsync("ReceiveDraftCompleted", evt, cancellationToken: ct);

        _webhookStream.Publish(new TournamentWebhookEventDto(
            tournamentId,
            string.Empty,
            "DraftCompleted",
            "O Draft de jogadores foi concluído e as equipes estão prontas!",
            evt.NewDraftVersion,
            "DraftCompleted",
            null,
            null,
            null,
            DateTime.UtcNow));
    }

    public async Task NotifyTournamentWebhookAsync(TournamentWebhookEventDto evt, CancellationToken ct = default)
    {
        // 1. Dispara no Stream HTTP SSE (estilo Webhook Push leve sem WebSocket)
        _webhookStream.Publish(evt);

        // 2. Dispara também um ping minúsculo para quem já estiver conectado no grupo SignalR
        string group = TournamentDraftHub.GetGroupName(evt.TournamentId);
        await _hubContext.Clients.Group(group).SendAsync("ReceiveTournamentWebhook", evt, cancellationToken: ct);
    }

    public async Task NotifySecuritySettingsChangedAsync(Tournaments.Contracts.Security.SystemSecuritySettingsDto settings, CancellationToken ct = default)
    {
        _logger.LogInformation("Notificando alteração global de configurações de segurança (Manutenção: {Maintenance})", settings.MaintenanceMode);

        _webhookStream.Publish(new TournamentWebhookEventDto(
            Guid.Empty,
            "Sistema",
            "SecuritySettingsUpdated",
            settings.MaintenanceMode ? "Modo manutenção do sistema ativado." : "Configurações de segurança globais atualizadas.",
            1,
            settings.MaintenanceMode ? "Maintenance" : "Active",
            null,
            null,
            null,
            DateTime.UtcNow));

        await _hubContext.Clients.All.SendAsync("ReceiveSecuritySettingsChanged", settings, cancellationToken: ct);
    }
}
