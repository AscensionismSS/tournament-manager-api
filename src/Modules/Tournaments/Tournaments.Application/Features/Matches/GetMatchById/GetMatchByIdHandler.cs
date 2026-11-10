using MediatR;
using Tournaments.Contracts.Matches;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Matches.GetMatchById;

public class GetMatchByIdHandler : IRequestHandler<GetMatchByIdQuery, MatchSeriesDto?>
{
    private readonly IMatchSeriesRepository _matchRepository;

    public GetMatchByIdHandler(IMatchSeriesRepository matchRepository)
    {
        _matchRepository = matchRepository;
    }

    public async Task<MatchSeriesDto?> Handle(GetMatchByIdQuery request, CancellationToken cancellationToken)
    {
        var series = await _matchRepository.GetByIdAsync(request.Id, cancellationToken);
        return series?.ToDto();
    }
}
