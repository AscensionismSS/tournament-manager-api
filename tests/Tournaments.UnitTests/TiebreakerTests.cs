using Tournaments.Domain.Services;
using Xunit;

namespace Tournaments.UnitTests;

public class TiebreakerTests
{
    [Fact]
    public void SortStandings_HigherPoints_WinsFirst()
    {
        var team1 = new TeamStanding(Guid.NewGuid(), "Time 1", Points: 6, MapWins: 4, MapLosses: 0, TotalVictoryDurationSeconds: 3000, TotalKills: 50, TotalDeaths: 20, new Dictionary<Guid, int>());
        var team2 = new TeamStanding(Guid.NewGuid(), "Time 2", Points: 3, MapWins: 2, MapLosses: 2, TotalVictoryDurationSeconds: 2000, TotalKills: 60, TotalDeaths: 10, new Dictionary<Guid, int>());

        var sorted = GroupStandingsEvaluator.SortStandings([team2, team1]);

        Assert.Equal("Time 1", sorted[0].TeamName);
        Assert.Equal("Time 2", sorted[1].TeamName);
    }

    [Fact]
    public void SortStandings_EqualPoints_HigherMapDifferentialWins()
    {
        var team1 = new TeamStanding(Guid.NewGuid(), "Time 1", Points: 3, MapWins: 3, MapLosses: 1, TotalVictoryDurationSeconds: 3000, TotalKills: 50, TotalDeaths: 20, new Dictionary<Guid, int>()); // diff +2
        var team2 = new TeamStanding(Guid.NewGuid(), "Time 2", Points: 3, MapWins: 2, MapLosses: 2, TotalVictoryDurationSeconds: 2000, TotalKills: 60, TotalDeaths: 10, new Dictionary<Guid, int>()); // diff 0

        var sorted = GroupStandingsEvaluator.SortStandings([team2, team1]);

        Assert.Equal("Time 1", sorted[0].TeamName);
        Assert.Equal("Time 2", sorted[1].TeamName);
    }

    [Fact]
    public void SortStandings_EqualPointsAndDiff_HeadToHeadWins()
    {
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();

        var teamA = new TeamStanding(teamAId, "Time A", Points: 3, MapWins: 2, MapLosses: 2, TotalVictoryDurationSeconds: 4000, TotalKills: 30, TotalDeaths: 30,
            new Dictionary<Guid, int> { [teamBId] = 1 }); // A venceu B

        var teamB = new TeamStanding(teamBId, "Time B", Points: 3, MapWins: 2, MapLosses: 2, TotalVictoryDurationSeconds: 2000, TotalKills: 40, TotalDeaths: 20,
            new Dictionary<Guid, int> { [teamAId] = 0 });

        var sorted = GroupStandingsEvaluator.SortStandings([teamB, teamA]);

        // Mesmo Time B tendo melhor tempo e mais kills, Time A vence no confronto direto!
        Assert.Equal("Time A", sorted[0].TeamName);
        Assert.Equal("Time B", sorted[1].TeamName);
    }

    [Fact]
    public void SortStandings_CircularTripleTie_SkipsH2HAndAppliesMetrics()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();

        // Ciclo: A venceu B, B venceu C, C venceu A
        var teamA = new TeamStanding(idA, "Time A", Points: 3, MapWins: 2, MapLosses: 2,
            TotalVictoryDurationSeconds: 1500, // Vitória mais rápida!
            TotalKills: 40, TotalDeaths: 30,
            new Dictionary<Guid, int> { [idB] = 1, [idC] = 0 });

        var teamB = new TeamStanding(idB, "Time B", Points: 3, MapWins: 2, MapLosses: 2,
            TotalVictoryDurationSeconds: 2500,
            TotalKills: 50, TotalDeaths: 30,
            new Dictionary<Guid, int> { [idC] = 1, [idA] = 0 });

        var teamC = new TeamStanding(idC, "Time C", Points: 3, MapWins: 2, MapLosses: 2,
            TotalVictoryDurationSeconds: 3500,
            TotalKills: 40, TotalDeaths: 40,
            new Dictionary<Guid, int> { [idA] = 1, [idB] = 0 });

        var sorted = GroupStandingsEvaluator.SortStandings([teamC, teamB, teamA]);

        // Como o ciclo é fechado, o critério 4 (menor tempo de vitória) desempata: A (1500s) -> B (2500s) -> C (3500s)
        Assert.Equal("Time A", sorted[0].TeamName);
        Assert.Equal("Time B", sorted[1].TeamName);
        Assert.Equal("Time C", sorted[2].TeamName);
    }
}
