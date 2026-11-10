using MediatR;
using Tournaments.Contracts.Draft;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Tournaments.GetTournamentById;

public class GetTournamentByIdHandler : IRequestHandler<GetTournamentByIdQuery, TournamentDto?>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IDraftSessionRepository? _draftRepository;

    public GetTournamentByIdHandler(
        ITournamentRepository tournamentRepository,
        IDraftSessionRepository? draftRepository = null)
    {
        _tournamentRepository = tournamentRepository;
        _draftRepository = draftRepository;
    }

    public async Task<TournamentDto?> Handle(GetTournamentByIdQuery request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (tournament == null) return null;

        int? playerCount = null;
        IReadOnlyList<DraftPlayerDto>? playerPool = null;

        if (tournament.DraftSessionId.HasValue && _draftRepository != null)
        {
            var session = await _draftRepository.GetByIdAsync(tournament.DraftSessionId.Value, cancellationToken);
            if (session != null)
            {
                playerCount = session.PlayerPool.Count;
                playerPool = session.PlayerPool.Select(p =>
                {
                    string? friendId = null;
                    string? steamUrl = null;
                    if (ulong.TryParse(p.ExternalUserId, out var steam64) && steam64 > 76561197960265728UL)
                    {
                        friendId = ((uint)(steam64 - 76561197960265728UL)).ToString();
                        steamUrl = $"https://steamcommunity.com/profiles/{steam64}";
                    }

                    return new DraftPlayerDto(
                        p.Id,
                        p.Name,
                        p.ExternalUserId,
                        p.Roles,
                        p.PreferredRole,
                        p.RankTier,
                        null,
                        p.AvatarUrl,
                        friendId,
                        steamUrl
                    );
                }).ToList();
            }
        }

        return tournament.ToDto(playerCount, playerPool);
    }
}
