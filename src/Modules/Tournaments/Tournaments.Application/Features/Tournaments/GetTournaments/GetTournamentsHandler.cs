using MediatR;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.GetTournaments;

public class GetTournamentsHandler : IRequestHandler<GetTournamentsQuery, IReadOnlyList<TournamentDto>>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IDraftSessionRepository? _draftRepository;

    public GetTournamentsHandler(
        ITournamentRepository tournamentRepository,
        IDraftSessionRepository? draftRepository = null)
    {
        _tournamentRepository = tournamentRepository;
        _draftRepository = draftRepository;
    }

    public async Task<IReadOnlyList<TournamentDto>> Handle(GetTournamentsQuery request, CancellationToken cancellationToken)
    {
        var tournaments = await _tournamentRepository.GetAllAsync(cancellationToken);

        if (request.Status.HasValue)
        {
            tournaments = tournaments.Where(t => t.Status == request.Status.Value).ToList();
        }

        var result = new List<TournamentDto>();
        foreach (var t in tournaments.OrderByDescending(t => t.CreatedAtUtc))
        {
            int? playerCount = null;
            if (t.DraftSessionId.HasValue && _draftRepository != null)
            {
                var session = await _draftRepository.GetByIdAsync(t.DraftSessionId.Value, cancellationToken);
                playerCount = session?.PlayerPool.Count;
            }

            result.Add(t.ToDto(playerCount));
        }

        return result;
    }
}
