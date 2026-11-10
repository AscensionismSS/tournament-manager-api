using Microsoft.EntityFrameworkCore;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Repositories;
using Tournaments.Infrastructure.Persistence;

namespace Tournaments.Infrastructure.Repositories;

public class EfDraftSessionRepository : IDraftSessionRepository
{
    private readonly TournamentDbContext _dbContext;

    public EfDraftSessionRepository(TournamentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DraftSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DraftSessions
            .Include(s => s.Teams)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<DraftSession?> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DraftSessions
            .Include(s => s.Teams)
            .FirstOrDefaultAsync(s => s.TournamentId == tournamentId, cancellationToken);
    }

    public async Task AddAsync(DraftSession session, CancellationToken cancellationToken = default)
    {
        await _dbContext.DraftSessions.AddAsync(session, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DraftSession session, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(session);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.DraftSessions.Attach(session);
            entry.State = EntityState.Modified;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var dbEntry in ex.Entries)
            {
                var databaseValues = await dbEntry.GetDatabaseValuesAsync(cancellationToken);
                if (databaseValues != null)
                {
                    dbEntry.OriginalValues.SetValues(databaseValues);
                }
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(DraftSession session, CancellationToken cancellationToken = default)
    {
        _dbContext.DraftSessions.Remove(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
