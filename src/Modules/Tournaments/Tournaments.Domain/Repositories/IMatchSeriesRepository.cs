using Tournaments.Domain.Entities;

namespace Tournaments.Domain.Repositories;

public interface IMatchSeriesRepository
{
    Task<MatchSeries?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchSeries>> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task AddAsync(MatchSeries series, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<MatchSeries> seriesList, CancellationToken cancellationToken = default);
    Task UpdateAsync(MatchSeries series, CancellationToken cancellationToken = default);
    Task RemoveRangeAsync(IEnumerable<MatchSeries> seriesList, CancellationToken cancellationToken = default);
}
