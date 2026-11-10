using MediatR;

namespace Tournaments.Application.Features.Matches.RecordWalkover;

public record RecordWalkoverCommand(Guid MatchSeriesId, Guid WinnerTeamId) : IRequest;
