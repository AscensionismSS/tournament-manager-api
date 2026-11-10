namespace Tournaments.Domain.Services;

public record TeamStanding(
    Guid TeamId,
    string TeamName,
    int Points,
    int MapWins,
    int MapLosses,
    long TotalVictoryDurationSeconds,
    int TotalKills,
    int TotalDeaths,
    IReadOnlyDictionary<Guid, int> HeadToHeadWins,
    int PlayedMatches = 0,
    int WonMatches = 0,
    int DrawnMatches = 0,
    int LostMatches = 0
)
{
    public int MapDifferential => MapWins - MapLosses;
    public int KillDifferential => TotalKills - TotalDeaths;
}

public static class GroupStandingsEvaluator
{
    public static IReadOnlyList<TeamStanding> SortStandings(IReadOnlyList<TeamStanding> teams)
    {
        if (teams.Count <= 1)
            return teams;

        return ResolveGroup(teams.ToList());
    }

    private static List<TeamStanding> ResolveGroup(List<TeamStanding> group)
    {
        if (group.Count <= 1)
            return group;

        // Critério 1: Pontuação Total (Maior vence)
        var byPoints = group
            .GroupBy(t => t.Points)
            .OrderByDescending(g => g.Key);

        var result = new List<TeamStanding>();
        foreach (var pointsGroup in byPoints)
        {
            var pList = pointsGroup.ToList();
            if (pList.Count == 1)
            {
                result.AddRange(pList);
                continue;
            }

            // Critério 2: Saldo de Mapas (MapDifferential) (Maior vence)
            var byMapDiff = pList
                .GroupBy(t => t.MapDifferential)
                .OrderByDescending(g => g.Key);

            foreach (var diffGroup in byMapDiff)
            {
                var dList = diffGroup.ToList();
                if (dList.Count == 1)
                {
                    result.AddRange(dList);
                    continue;
                }

                // Critério 3: Confronto Direto (Head-to-Head)
                var resolvedH2H = ResolveHeadToHead(dList);
                result.AddRange(resolvedH2H);
            }
        }

        return result;
    }

    private static List<TeamStanding> ResolveHeadToHead(List<TeamStanding> tied)
    {
        if (tied.Count == 2)
        {
            var a = tied[0];
            var b = tied[1];
            a.HeadToHeadWins.TryGetValue(b.TeamId, out int aWinsOverB);
            b.HeadToHeadWins.TryGetValue(a.TeamId, out int bWinsOverA);

            if (aWinsOverB > bWinsOverA)
                return [a, b];
            if (bWinsOverA > aWinsOverB)
                return [b, a];

            // Empatados no 1v1 -> desempate por métricas (Critério 4)
            return ResolveByMetrics(tied);
        }

        // Se tied.Count >= 3:
        // Calcula vitórias internas apenas entre os times do subgrupo empatado
        var subgroupWins = new Dictionary<Guid, int>();
        foreach (var team in tied)
        {
            int wins = 0;
            foreach (var opp in tied)
            {
                if (opp.TeamId == team.TeamId) continue;
                if (team.HeadToHeadWins.TryGetValue(opp.TeamId, out int w))
                {
                    wins += w;
                }
            }
            subgroupWins[team.TeamId] = wins;
        }

        // Verifica se há separação (nem todos têm a mesma quantidade de vitórias internas)
        var distinctWinsCount = subgroupWins.Values.Distinct().Count();
        if (distinctWinsCount > 1)
        {
            // O confronto direto separa pelo menos uma parte dos times.
            // Agrupa por vitórias no mini-torneio interno em ordem decrescente
            var byInternalWins = tied
                .GroupBy(t => subgroupWins[t.TeamId])
                .OrderByDescending(g => g.Key);

            var resolved = new List<TeamStanding>();
            foreach (var group in byInternalWins)
            {
                var gList = group.ToList();
                if (gList.Count == 1)
                {
                    resolved.AddRange(gList);
                }
                else if (gList.Count < tied.Count)
                {
                    // Recursivamente resolve o subgrupo menor
                    resolved.AddRange(ResolveHeadToHead(gList));
                }
                else
                {
                    // Todos continuam empatados
                    resolved.AddRange(ResolveByMetrics(gList));
                }
            }
            return resolved;
        }

        // Se todos os times do subgrupo têm exatamente o mesmo número de vitórias internas (empate circular perfeito):
        // Pula Critério 3 e desempata por métricas (Critério 4)
        return ResolveByMetrics(tied);
    }

    private static List<TeamStanding> ResolveByMetrics(List<TeamStanding> tied)
    {
        var list = tied.ToList();
        list.Sort((a, b) =>
        {
            // Critério 4: Menor Tempo em Vitórias (apenas se ambos > 0)
            if (a.TotalVictoryDurationSeconds > 0 && b.TotalVictoryDurationSeconds > 0)
            {
                int durationComparison = a.TotalVictoryDurationSeconds.CompareTo(b.TotalVictoryDurationSeconds);
                if (durationComparison != 0) return durationComparison;
            }
            else if (a.TotalVictoryDurationSeconds > 0 && b.TotalVictoryDurationSeconds <= 0)
            {
                return -1;
            }
            else if (b.TotalVictoryDurationSeconds > 0 && a.TotalVictoryDurationSeconds <= 0)
            {
                return 1;
            }

            // Maior Saldo de Kills
            int killDiffComparison = b.KillDifferential.CompareTo(a.KillDifferential);
            if (killDiffComparison != 0) return killDiffComparison;

            // Desempate estável e determinístico por TeamId
            return a.TeamId.CompareTo(b.TeamId);
        });

        return list;
    }
}
