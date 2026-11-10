using MediatR;
using Tournaments.Contracts.Tournaments;

namespace Tournaments.Application.Features.Tournaments.GetTournamentStandings;

public record GetTournamentStandingsQuery(Guid TournamentId) : IRequest<TournamentStandingsDto>;
