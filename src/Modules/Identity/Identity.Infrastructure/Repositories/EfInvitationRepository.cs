using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class EfInvitationRepository : IInvitationRepository
{
    private readonly IdentityDbContext _context;

    public EfInvitationRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Invitations.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<Invitation?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.Invitations.FirstOrDefaultAsync(i => i.Code == code.Trim(), ct);
    }

    public async Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Invitations
            .Where(i => i.Email == email.Trim().ToLowerInvariant() && i.UsedAtUtc == null)
            .OrderByDescending(i => i.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Invitation invitation, CancellationToken ct = default)
    {
        await _context.Invitations.AddAsync(invitation, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Invitation invitation, CancellationToken ct = default)
    {
        _context.Invitations.Update(invitation);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Invitation>> GetPendingAsync(CancellationToken ct = default)
    {
        return await _context.Invitations
            .Where(i => i.UsedAtUtc == null && DateTime.UtcNow <= i.ExpiresAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Invitation>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Invitations
            .OrderByDescending(i => i.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
