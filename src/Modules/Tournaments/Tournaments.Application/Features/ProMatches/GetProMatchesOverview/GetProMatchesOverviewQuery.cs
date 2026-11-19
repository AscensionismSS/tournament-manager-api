using MediatR;
using Tournaments.Contracts.ProMatches;

namespace Tournaments.Application.Features.ProMatches.GetProMatchesOverview;

public record GetProMatchesOverviewQuery() : IRequest<ProMatchesOverviewDto>;
