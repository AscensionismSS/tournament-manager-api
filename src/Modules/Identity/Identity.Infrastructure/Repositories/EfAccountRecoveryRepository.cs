using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class EfAccountRecoveryRepository : IAccountRecoveryRepository
{
    private readonly IdentityDbContext _context;

    public EfAccountRecoveryRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<AccountRecoveryToken?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.RecoveryTokens
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<AccountRecoveryToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await _context.RecoveryTokens
            .FirstOrDefaultAsync(t => t.Token == token.Trim().ToLowerInvariant(), ct);
    }

    public async Task<IReadOnlyList<AccountRecoveryToken>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.RecoveryTokens
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(AccountRecoveryToken token, CancellationToken ct = default)
    {
        await _context.RecoveryTokens.AddAsync(token, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AccountRecoveryToken token, CancellationToken ct = default)
    {
        _context.RecoveryTokens.Update(token);
        await _context.SaveChangesAsync(ct);
    }
}
