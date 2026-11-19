using Tournaments.Contracts.ProMatches;

namespace Tournaments.Application.Common.Interfaces;

public interface IProMatchesService
{
    Task<ProMatchesOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
