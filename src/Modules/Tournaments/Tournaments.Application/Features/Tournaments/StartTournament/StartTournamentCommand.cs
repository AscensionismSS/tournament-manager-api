using MediatR;

namespace Tournaments.Application.Features.Tournaments.StartTournament;

public record StartTournamentCommand(Guid TournamentId) : IRequest;
