using MediatR;

namespace Tournaments.Application.Features.Matches.RemoveLastGame;

public record RemoveLastGameCommand(Guid MatchSeriesId) : IRequest;
