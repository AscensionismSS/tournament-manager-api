using MediatR;
using Tournaments.Application.Features.Matches;
using Tournaments.Contracts.Matches;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Matches.GetTournamentMatches;

// O Handler é o cozinheiro: ele recebe a Query, vai no repositório do banco,
// pega as partidas e converte para DTO usando o MatchMapper que preservamos!
public class GetTournamentMatchesHandler : IRequestHandler<GetTournamentMatchesQuery, IReadOnlyList<MatchSeriesDto>>
{
    private readonly IMatchSeriesRepository _matchRepository;

    public GetTournamentMatchesHandler(IMatchSeriesRepository matchRepository)
    {
        _matchRepository = matchRepository;
    }

    public async Task<IReadOnlyList<MatchSeriesDto>> Handle(
        GetTournamentMatchesQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Busca todas as partidas daquele torneio no banco
        var matches = await _matchRepository.GetByTournamentIdAsync(request.TournamentId, cancellationToken);

        // 2. Transforma as entidades do banco em DTOs prontos para a tela
        return matches.Select(m => m.ToDto()).ToList();
    }
}
