using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class EfUserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public EfUserRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant(), ct);
    }

    public async Task<User?> GetByIdentifierAsync(string identifier, CancellationToken ct = default)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        return await _context.Users.FirstOrDefaultAsync(u =>
            u.Email == normalized || u.DisplayName.ToLower() == normalized, ct);
    }

    public async Task<User?> GetBySteamIdAsync(ulong steamId, CancellationToken ct = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.SteamId == steamId, ct);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Users.AnyAsync(u => u.Email == email.Trim().ToLowerInvariant(), ct);
    }

    public async Task<bool> ExistsByDisplayNameAsync(string displayName, CancellationToken ct = default)
    {
        var normalized = displayName.Trim().ToLower();
        return await _context.Users.AnyAsync(u => u.DisplayName.ToLower() == normalized, ct);
    }

    public async Task<bool> ExistsBySteamIdAsync(ulong steamId, CancellationToken ct = default)
    {
        return await _context.Users.AnyAsync(u => u.SteamId == steamId, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Users.AsNoTracking().ToListAsync(ct);
    }
}
