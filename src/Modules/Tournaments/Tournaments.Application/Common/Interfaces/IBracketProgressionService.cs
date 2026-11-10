using Tournaments.Domain.Entities;

namespace Tournaments.Application.Common.Interfaces;

public interface IBracketProgressionService
{
    Task ProgressBracketAsync(MatchSeries completedSeries, CancellationToken cancellationToken = default);
    Task RollbackBracketProgressionAsync(MatchSeries seriesToRollback, CancellationToken cancellationToken = default);
    Task UndoProgressionOnlyAsync(MatchSeries seriesToUndo, CancellationToken cancellationToken = default);
}
