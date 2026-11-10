using System.Collections.Concurrent;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;

namespace Identity.Infrastructure.Repositories;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        var user = _users.Values.FirstOrDefault(u => u.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdentifierAsync(string identifier, CancellationToken ct = default)
    {
        string normalized = identifier.Trim().ToLowerInvariant();
        var user = _users.Values.FirstOrDefault(u =>
            u.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            u.DisplayName.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<User?> GetBySteamIdAsync(ulong steamId, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => u.SteamId == steamId);
        return Task.FromResult(user);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        bool exists = _users.Values.Any(u => u.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task<bool> ExistsByDisplayNameAsync(string displayName, CancellationToken ct = default)
    {
        string normalized = displayName.Trim().ToLowerInvariant();
        bool exists = _users.Values.Any(u => u.DisplayName.ToLower().Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task<bool> ExistsBySteamIdAsync(ulong steamId, CancellationToken ct = default)
    {
        bool exists = _users.Values.Any(u => u.SteamId == steamId);
        return Task.FromResult(exists);
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<User> list = _users.Values.ToList();
        return Task.FromResult(list);
    }
}

public class InMemoryInvitationRepository : IInvitationRepository
{
    private readonly ConcurrentDictionary<Guid, Invitation> _invitations = new();

    public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _invitations.TryGetValue(id, out var invitation);
        return Task.FromResult(invitation);
    }

    public Task<Invitation?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        string trimmed = code.Trim();
        var invitation = _invitations.Values.FirstOrDefault(i => i.Code.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(invitation);
    }

    public Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken ct = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        var invitation = _invitations.Values
            .Where(i => i.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase) && !i.IsUsed)
            .OrderByDescending(i => i.CreatedAtUtc)
            .FirstOrDefault();

        return Task.FromResult(invitation);
    }

    public Task AddAsync(Invitation invitation, CancellationToken ct = default)
    {
        _invitations[invitation.Id] = invitation;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Invitation invitation, CancellationToken ct = default)
    {
        _invitations[invitation.Id] = invitation;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Invitation>> GetPendingAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Invitation> list = _invitations.Values
            .Where(i => !i.IsUsed && !i.IsExpired)
            .ToList();

        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<Invitation>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Invitation> list = _invitations.Values
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToList();

        return Task.FromResult(list);
    }
}

public class InMemoryAccountRecoveryRepository : IAccountRecoveryRepository
{
    private readonly ConcurrentDictionary<Guid, AccountRecoveryToken> _tokens = new();

    public Task<AccountRecoveryToken?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _tokens.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<AccountRecoveryToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        string normalized = token.Trim().ToLowerInvariant();
        var item = _tokens.Values.FirstOrDefault(t => t.Token.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<AccountRecoveryToken>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<AccountRecoveryToken> list = _tokens.Values
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(AccountRecoveryToken token, CancellationToken ct = default)
    {
        _tokens[token.Id] = token;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AccountRecoveryToken token, CancellationToken ct = default)
    {
        _tokens[token.Id] = token;
        return Task.CompletedTask;
    }
}
