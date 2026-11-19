using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.ProMatches;

namespace Tournaments.Application.Features.ProMatches.GetProMatchesOverview;

public class GetProMatchesOverviewHandler : IRequestHandler<GetProMatchesOverviewQuery, ProMatchesOverviewDto>
{
    private readonly IProMatchesService _proMatchesService;

    public GetProMatchesOverviewHandler(IProMatchesService proMatchesService)
    {
        _proMatchesService = proMatchesService;
    }

    public Task<ProMatchesOverviewDto> Handle(GetProMatchesOverviewQuery request, CancellationToken cancellationToken) =>
        _proMatchesService.GetOverviewAsync(cancellationToken);
}
