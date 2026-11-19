using Identity.Application.Common.Interfaces;
using Identity.Domain.Repositories;
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
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? Context.User.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId) && userId != Guid.Empty)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
                _logger.LogInformation("Cliente {ConnectionId} autenticado associado automaticamente ao grupo {Group}", Context.ConnectionId, GetUserGroupName(userId));
            }
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
        await ValidateUserChannelAccessAsync(userId, "Acesso negado: Você não tem permissão para assinar o canal pessoal de outro usuário.");

        string groupName = GetUserGroupName(userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Cliente {ConnectionId} conectado ao canal pessoal do usuário {UserId}", Context.ConnectionId, userId);
    }

    public async Task LeaveUserChannel(Guid userId)
    {
        await ValidateUserChannelAccessAsync(userId, "Acesso negado: Você não tem permissão para gerenciar o canal pessoal de outro usuário.");

        string groupName = GetUserGroupName(userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Cliente {ConnectionId} desconectado do canal pessoal do usuário {UserId}", Context.ConnectionId, userId);
    }

    private async Task ValidateUserChannelAccessAsync(Guid targetUserId, string accessDeniedMessage)
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Tentativa não autenticada de acessar canal pessoal. Conexão {ConnectionId}, Usuário Solicitado {TargetUserId}", Context.ConnectionId, targetUserId);
            throw new HubException(accessDeniedMessage);
        }

        var callerUserId = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? Context.User.FindFirst("sub")?.Value;

        bool isAdmin = Context.User.IsInRole("SuperAdmin") || Context.User.IsInRole("TournamentAdmin");

        if (!Guid.TryParse(callerUserId, out var authenticatedUserId) ||
            authenticatedUserId == Guid.Empty)
        {
            _logger.LogWarning("Tentativa não autorizada de acessar canal pessoal. Conexão {ConnectionId}, Usuário Solicitado {TargetUserId}", Context.ConnectionId, targetUserId);
            throw new HubException(accessDeniedMessage);
        }

        var requestServices = Context.GetHttpContext()?.RequestServices;
        if (requestServices?.GetService<IUserAuthStateCache>() is { } authStateCache &&
            HasUserRepositoryRegistered(requestServices))
        {
            var snapshot = await authStateCache.GetOrLoadAsync(
                authenticatedUserId,
                async ct =>
                {
                    var scopeFactory = requestServices.GetService<IServiceScopeFactory>();
                    if (scopeFactory is not null)
                    {
                        using var scope = scopeFactory.CreateScope();
                        var scopedRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                        var scopedUser = await scopedRepo.GetByIdAsync(authenticatedUserId, ct);
                        return scopedUser is null
                            ? new UserAuthSnapshot(false, false, string.Empty)
                            : new UserAuthSnapshot(true, scopedUser.IsActive, scopedUser.Role.ToString());
                    }

                    var repo = requestServices.GetRequiredService<IUserRepository>();
                    var user = await repo.GetByIdAsync(authenticatedUserId, ct);
                    return user is null
                        ? new UserAuthSnapshot(false, false, string.Empty)
                        : new UserAuthSnapshot(true, user.IsActive, user.Role.ToString());
                },
                Context.ConnectionAborted);

            if (!snapshot.Exists || !snapshot.IsActive)
            {
                Context.Abort();
                throw new HubException("Sua conta está desativada.");
            }

            if (!string.IsNullOrWhiteSpace(snapshot.Role))
            {
                isAdmin = string.Equals(snapshot.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(snapshot.Role, "TournamentAdmin", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (authenticatedUserId != targetUserId && !isAdmin)
        {
            _logger.LogWarning("Tentativa não autorizada de acessar canal pessoal. Conexão {ConnectionId}, Usuário Solicitado {TargetUserId}", Context.ConnectionId, targetUserId);
            throw new HubException(accessDeniedMessage);
        }
    }

    private static bool HasUserRepositoryRegistered(IServiceProvider requestServices)
    {
        if (requestServices.GetService<IServiceScopeFactory>() is { } scopeFactory)
        {
            using var scope = scopeFactory.CreateScope();
            return scope.ServiceProvider.GetService<IUserRepository>() is not null;
        }

        return requestServices.GetService<IUserRepository>() is not null;
    }

    public static string GetUserGroupName(Guid userId) => $"user-{userId}";
}
