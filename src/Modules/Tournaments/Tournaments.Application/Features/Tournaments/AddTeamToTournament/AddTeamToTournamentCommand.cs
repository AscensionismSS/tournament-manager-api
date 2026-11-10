using MediatR;

namespace Tournaments.Application.Features.Tournaments.AddTeamToTournament;

public record AddTeamToTournamentCommand(Guid TournamentId, Guid TeamId, string TeamName) : IRequest;
