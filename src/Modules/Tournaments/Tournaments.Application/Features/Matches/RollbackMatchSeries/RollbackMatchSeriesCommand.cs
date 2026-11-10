using MediatR;

namespace Tournaments.Application.Features.Matches.RollbackMatchSeries;

public record RollbackMatchSeriesCommand(Guid MatchSeriesId) : IRequest;
