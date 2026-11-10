using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.CancelTournament;

public record CancelTournamentCommand(Guid TournamentId) : IRequest<TournamentDto>;

