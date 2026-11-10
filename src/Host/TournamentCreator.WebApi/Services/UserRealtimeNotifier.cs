using Identity.Application.Common.Interfaces;
using Identity.Contracts.Auth;
using Microsoft.AspNetCore.SignalR;
using TournamentCreator.WebApi.Hubs.v1;

namespace TournamentCreator.WebApi.Services;

public class UserRealtimeNotifier : IUserRealtimeNotifier
{
    private readonly IHubContext<TournamentDraftHub> _hubContext;
    private readonly ILogger<UserRealtimeNotifier> _logger;

    public UserRealtimeNotifier(IHubContext<TournamentDraftHub> hubContext, ILogger<UserRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifySteamUnlockedAsync(Guid userId, UserDto updatedUser, CancellationToken ct = default)
    {
        string group = TournamentDraftHub.GetUserGroupName(userId);
        _logger.LogInformation("Enviando notificação em tempo real de desbloqueio de Steam para usuário {UserId} no grupo {Group}", userId, group);
        await _hubContext.Clients.Group(group).SendAsync("ReceiveSteamUnlocked", updatedUser, cancellationToken: ct);
        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveSteamUnlocked", updatedUser, cancellationToken: ct);
    }
}
