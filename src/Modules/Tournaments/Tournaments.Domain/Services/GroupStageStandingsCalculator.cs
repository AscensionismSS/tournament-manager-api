using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;

namespace Tournaments.Domain.Services;

public static class GroupStageStandingsCalculator
{
    public static IReadOnlyList<TeamStanding> Calculate(
        IReadOnlyList<TournamentTeam> teams,
        IReadOnlyList<MatchSeries> matches)
    {
        if (teams.Count == 0)
            return [];

        // Inicializa dicionário de acumuladores por equipe
        var statsByTeam = teams.ToDictionary(
            t => t.Id,
            t => new MutableTeamStats(t.Id, t.Name)
        );

        foreach (var series in matches)
        {
            if (!series.TeamAId.HasValue || !series.TeamBId.HasValue)
                continue;

            var teamAId = series.TeamAId.Value;
            var teamBId = series.TeamBId.Value;

            if (!statsByTeam.ContainsKey(teamAId) || !statsByTeam.ContainsKey(teamBId))
                continue;

            var statsA = statsByTeam[teamAId];
            var statsB = statsByTeam[teamBId];

            // 1. Processa cada mapa individual da série
            foreach (var game in series.Games)
            {
                if (game.WinnerTeamId == teamAId)
                {
                    statsA.MapWins++;
                    statsA.TotalKills += game.TeamAKills;
                    statsA.TotalDeaths += game.TeamBKills;
                    if (!game.IsWalkover)
                    {
                        statsA.TotalVictoryDurationSeconds += game.DurationSeconds;
                    }

                    statsB.MapLosses++;
                    statsB.TotalKills += game.TeamBKills;
                    statsB.TotalDeaths += game.TeamAKills;
                }
                else if (game.WinnerTeamId == teamBId)
                {
                    statsB.MapWins++;
                    statsB.TotalKills += game.TeamBKills;
                    statsB.TotalDeaths += game.TeamAKills;
                    if (!game.IsWalkover)
                    {
                        statsB.TotalVictoryDurationSeconds += game.DurationSeconds;
                    }

                    statsA.MapLosses++;
                    statsA.TotalKills += game.TeamAKills;
                    statsA.TotalDeaths += game.TeamBKills;
                }
            }

            // 2. Processa o resultado final da série quando concluída
            if (series.Status == MatchSeriesStatus.Completed)
            {
                statsA.PlayedMatches++;
                statsB.PlayedMatches++;

                if (series.Format == SeriesFormat.Bo2)
                {
                    // Regra MD2 (Padrão Dota 2)
                    if (series.ScoreTeamA == 2)
                    {
                        statsA.WonMatches++;
                        statsA.Points += 3;
                        statsB.LostMatches++;
                        statsA.RecordHeadToHeadWin(teamBId);
                    }
                    else if (series.ScoreTeamB == 2)
                    {
                        statsB.WonMatches++;
                        statsB.Points += 3;
                        statsA.LostMatches++;
                        statsB.RecordHeadToHeadWin(teamAId);
                    }
                    else if (series.ScoreTeamA == 1 && series.ScoreTeamB == 1)
                    {
                        // Empate 1x1: 1 ponto para cada equipe
                        statsA.DrawnMatches++;
                        statsA.Points += 1;
                        statsB.DrawnMatches++;
                        statsB.Points += 1;
                    }
                }
                else
                {
                    // Regras MD1, MD3, MD5
                    if (series.WinnerTeamId == teamAId)
                    {
                        statsA.WonMatches++;
                        statsA.Points += 3;
                        statsB.LostMatches++;
                        statsA.RecordHeadToHeadWin(teamBId);
                    }
                    else if (series.WinnerTeamId == teamBId)
                    {
                        statsB.WonMatches++;
                        statsB.Points += 3;
                        statsA.LostMatches++;
                        statsB.RecordHeadToHeadWin(teamAId);
                    }
                }
            }
        }

        // Converte os dados acumulados para a lista de TeamStanding
        var rawStandings = statsByTeam.Values
            .Select(s => s.ToStanding())
            .ToList();

        // Ordena aplicando rigorosamente os critérios de desempate oficiais
        return GroupStandingsEvaluator.SortStandings(rawStandings);
    }

    private class MutableTeamStats(Guid teamId, string teamName)
    {
        public Guid TeamId { get; } = teamId;
        public string TeamName { get; } = teamName;
        public int Points { get; set; }
        public int PlayedMatches { get; set; }
        public int WonMatches { get; set; }
        public int DrawnMatches { get; set; }
        public int LostMatches { get; set; }
        public int MapWins { get; set; }
        public int MapLosses { get; set; }
        public long TotalVictoryDurationSeconds { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }
        public Dictionary<Guid, int> HeadToHeadWins { get; } = new();

        public void RecordHeadToHeadWin(Guid opponentId)
        {
            HeadToHeadWins[opponentId] = HeadToHeadWins.GetValueOrDefault(opponentId, 0) + 1;
        }

        public TeamStanding ToStanding() => new(
            TeamId,
            TeamName,
            Points,
            MapWins,
            MapLosses,
            TotalVictoryDurationSeconds,
            TotalKills,
            TotalDeaths,
            HeadToHeadWins,
            PlayedMatches,
            WonMatches,
            DrawnMatches,
            LostMatches
        );
    }
}
