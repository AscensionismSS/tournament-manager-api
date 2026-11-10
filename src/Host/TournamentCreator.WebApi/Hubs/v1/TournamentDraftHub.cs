using Microsoft.AspNetCore.SignalR;

namespace TournamentCreator.WebApi.Hubs.v1;

public class TournamentDraftHub : Hub
{
    private readonly ILogger<TournamentDraftHub> _logger;

    public TournamentDraftHub(ILogger<TournamentDraftHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? Context.User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
            _logger.LogInformation("Cliente {ConnectionId} autenticado associado automaticamente ao grupo {Group}", Context.ConnectionId, GetUserGroupName(userId));
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinTournamentDraft(Guid tournamentId)
    {
        string groupName = GetGroupName(tournamentId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Cliente {ConnectionId} conectou ao grupo de draft do torneio {TournamentId}", Context.ConnectionId, tournamentId);
    }

    public async Task LeaveTournamentDraft(Guid tournamentId)
    {
        string groupName = GetGroupName(tournamentId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Cliente {ConnectionId} desconectou do grupo de draft do torneio {TournamentId}", Context.ConnectionId, tournamentId);
    }

    public static string GetGroupName(Guid tournamentId) => $"tournament-draft-{tournamentId}";

    public async Task JoinUserChannel(Guid userId)
    {
        var callerUserId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? Context.User?.FindFirst("sub")?.Value;

        bool isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("TournamentAdmin") == true;

        if (!Guid.TryParse(callerUserId, out var authenticatedUserId) ||
            (authenticatedUserId != userId && !isAdmin))
        {
            _logger.LogWarning("Tentativa não autorizada de assinar canal pessoal. Conexão {ConnectionId}, Usuário Solicitado {TargetUserId}", Context.ConnectionId, userId);
            throw new HubException("Acesso negado: Você não tem permissão para assinar o canal pessoal de outro usuário.");
        }

        string groupName = GetUserGroupName(userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Cliente {ConnectionId} conectado ao canal pessoal do usuário {UserId}", Context.ConnectionId, userId);
    }

    public async Task LeaveUserChannel(Guid userId)
    {
        var callerUserId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? Context.User?.FindFirst("sub")?.Value;

        bool isAdmin = Context.User?.IsInRole("SuperAdmin") == true || Context.User?.IsInRole("TournamentAdmin") == true;

        if (!Guid.TryParse(callerUserId, out var authenticatedUserId) ||
            (authenticatedUserId != userId && !isAdmin))
        {
            throw new HubException("Acesso negado: Você não tem permissão para gerenciar o canal pessoal de outro usuário.");
        }

        string groupName = GetUserGroupName(userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Cliente {ConnectionId} desconectado do canal pessoal do usuário {UserId}", Context.ConnectionId, userId);
    }

    public static string GetUserGroupName(Guid userId) => $"user-{userId}";
}
