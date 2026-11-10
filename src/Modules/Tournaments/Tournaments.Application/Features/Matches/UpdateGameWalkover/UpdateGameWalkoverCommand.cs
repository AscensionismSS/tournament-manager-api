using MediatR;

namespace Tournaments.Application.Features.Matches.UpdateGameWalkover;

public record UpdateGameWalkoverCommand(
    Guid MatchSeriesId,
    int GameNumber,
    Guid WinnerTeamId
) : IRequest;
