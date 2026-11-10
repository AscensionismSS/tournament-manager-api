using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Repositories;
using Tournaments.Domain.Services;

namespace Tournaments.Application.Features.Tournaments.GetTournamentStandings;

public class GetTournamentStandingsHandler : IRequestHandler<GetTournamentStandingsQuery, TournamentStandingsDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IMatchSeriesRepository _matchRepository;

    public GetTournamentStandingsHandler(
        ITournamentRepository tournamentRepository,
        IMatchSeriesRepository matchRepository)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
    }

    public async Task<TournamentStandingsDto> Handle(GetTournamentStandingsQuery request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException($"Torneio '{request.TournamentId}' não encontrado.");

        var matches = await _matchRepository.GetByTournamentIdAsync(request.TournamentId, cancellationToken);

        // Calcula a tabela de classificação oficial através do serviço de domínio
        var sortedStandings = GroupStageStandingsCalculator.Calculate(tournament.Teams.ToList(), matches);

        var standingDtos = sortedStandings
            .Select((s, index) =>
            {
                string? groupCode = null;
                if (tournament.FormatConfig.GroupCount == 2 && tournament.Teams.Count >= 2)
                {
                    int perGroup = tournament.Teams.Count / 2;
                    var isGroupA = tournament.Teams.Take(perGroup).Any(t => t.Id == s.TeamId);
                    groupCode = isGroupA ? "A" : "B";
                }

                return new TeamStandingDto(
                    Rank: index + 1,
                    TeamId: s.TeamId,
                    TeamName: s.TeamName,
                    Points: s.Points,
                    PlayedMatches: s.PlayedMatches,
                    WonMatches: s.WonMatches,
                    DrawnMatches: s.DrawnMatches,
                    LostMatches: s.LostMatches,
                    MapWins: s.MapWins,
                    MapLosses: s.MapLosses,
                    MapDifferential: s.MapDifferential,
                    TotalVictoryDurationSeconds: s.TotalVictoryDurationSeconds,
                    TotalKills: s.TotalKills,
                    TotalDeaths: s.TotalDeaths,
                    KillDifferential: s.KillDifferential,
                    GroupCode: groupCode
                );
            })
            .ToList();

        return new TournamentStandingsDto(
            TournamentId: tournament.Id,
            TournamentName: tournament.Name,
            Status: tournament.Status.ToString(),
            Version: tournament.Version,
            LastUpdatedAtUtc: tournament.LastUpdatedAtUtc,
            Standings: standingDtos
        );
    }
}
