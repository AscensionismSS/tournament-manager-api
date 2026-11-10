using MediatR;

namespace Tournaments.Application.Features.Tournaments.DeleteTournament;

public record DeleteTournamentCommand(Guid TournamentId) : IRequest;

