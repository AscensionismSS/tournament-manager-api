using MediatR;
using Tournaments.Contracts.Matches;

namespace Tournaments.Application.Features.Matches.GetMatchById;

public record GetMatchByIdQuery(Guid Id) : IRequest<MatchSeriesDto?>;
