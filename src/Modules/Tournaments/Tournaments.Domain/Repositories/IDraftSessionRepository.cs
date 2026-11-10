using Tournaments.Domain.Entities;

namespace Tournaments.Domain.Repositories;

public interface IDraftSessionRepository
{
    Task<DraftSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DraftSession?> GetByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task AddAsync(DraftSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(DraftSession session, CancellationToken cancellationToken = default);
    Task DeleteAsync(DraftSession session, CancellationToken cancellationToken = default);
}
