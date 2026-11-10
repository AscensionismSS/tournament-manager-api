using Microsoft.EntityFrameworkCore;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Repositories;
using Tournaments.Infrastructure.Persistence;

namespace Tournaments.Infrastructure.Repositories;

public class EfTournamentRepository : ITournamentRepository
{
    private readonly TournamentDbContext _dbContext;

    public EfTournamentRepository(TournamentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tournaments
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        await _dbContext.Tournaments.AddAsync(tournament, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }


    public async Task UpdateAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(tournament);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.Tournaments.Attach(tournament);
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

    public async Task DeleteAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        _dbContext.Tournaments.Remove(tournament);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
