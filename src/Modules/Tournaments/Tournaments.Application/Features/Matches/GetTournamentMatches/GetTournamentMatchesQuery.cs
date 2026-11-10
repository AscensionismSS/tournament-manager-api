using MediatR;
using Tournaments.Contracts.Matches;

namespace Tournaments.Application.Features.Matches.GetTournamentMatches;

// A Query é apenas o envelope que carrega o ID do torneio que queremos consultar
public record GetTournamentMatchesQuery(Guid TournamentId) : IRequest<IReadOnlyList<MatchSeriesDto>>;
