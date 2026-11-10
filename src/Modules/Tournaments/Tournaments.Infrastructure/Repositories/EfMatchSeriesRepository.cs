using Microsoft.EntityFrameworkCore;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Repositories;
using Tournaments.Infrastructure.Persistence;

namespace Tournaments.Infrastructure.Repositories;

public class EfMatchSeriesRepository : IMatchSeriesRepository
{
    private readonly TournamentDbContext _dbContext;

    public EfMatchSeriesRepository(TournamentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MatchSeries?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MatchSeries
            .Include(s => s.Games)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MatchSeries>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MatchSeries
            .Include(s => s.Games)
            .Where(s => s.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MatchSeries series, CancellationToken cancellationToken = default)
    {
        await _dbContext.MatchSeries.AddAsync(series, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<MatchSeries> seriesList, CancellationToken cancellationToken = default)
    {
        await _dbContext.MatchSeries.AddRangeAsync(seriesList, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MatchSeries series, CancellationToken cancellationToken = default)
    {
        var existingGameIds = await _dbContext.MatchGames
            .Where(g => g.MatchSeriesId == series.Id)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        var currentSeriesGameIds = series.Games.Select(g => g.Id).ToHashSet();

        // 1. Remove deleted games (e.g. from RemoveLastGame or Rollback)
        var toDeleteIds = existingGameIds.Where(id => !currentSeriesGameIds.Contains(id)).ToList();
        if (toDeleteIds.Count > 0)
        {
            var toDelete = await _dbContext.MatchGames
                .Where(g => toDeleteIds.Contains(g.Id))
                .ToListAsync(cancellationToken);
            _dbContext.MatchGames.RemoveRange(toDelete);
        }

        // 2. Synchronize current games (Add new ones, modify existing ones)
        foreach (var game in series.Games)
        {
            var isNew = !existingGameIds.Contains(game.Id);
            var gameEntry = _dbContext.Entry(game);

            if (isNew)
            {
                if (gameEntry.State != EntityState.Added)
                {
                    _dbContext.MatchGames.Add(game);
                }
            }
            else
            {
                if (gameEntry.State == EntityState.Detached)
                {
                    _dbContext.MatchGames.Attach(game);
                    gameEntry.State = EntityState.Modified;
                }
            }
        }

        // 3. Synchronize series root
        var entry = _dbContext.Entry(series);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.MatchSeries.Attach(series);
            entry.State = EntityState.Modified;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveRangeAsync(IEnumerable<MatchSeries> seriesList, CancellationToken cancellationToken = default)
    {
        _dbContext.MatchSeries.RemoveRange(seriesList);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
