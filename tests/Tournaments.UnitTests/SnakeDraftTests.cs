using Tournaments.Domain.Services;
using Xunit;

namespace Tournaments.UnitTests;

public class SnakeDraftTests
{
    [Fact]
    public void SnakeDraft_4Teams_FollowsExactSerpentineOrderWithDoublePicks()
    {
        int teamCount = 4;
        var expectedSequence = new[]
        {
            // Rodada 1
            0, 1, 2, 3,
            // Rodada 2 (virada com pick duplo do time 3)
            3, 2, 1, 0,
            // Rodada 3 (virada com pick duplo do time 0)
            0, 1, 2, 3,
            // Rodada 4 (virada com pick duplo do time 3)
            3, 2, 1, 0
        };

        int totalPicks = SnakeDraftCalculator.GetTotalStarterPicks(teamCount);
        Assert.Equal(16, totalPicks);

        for (int pickIndex = 0; pickIndex < totalPicks; pickIndex++)
        {
            int teamIndex = SnakeDraftCalculator.GetTeamIndexForPick(pickIndex, teamCount);
            Assert.Equal(expectedSequence[pickIndex], teamIndex);
        }
    }

    [Fact]
    public void SnakeDraft_8Teams_TotalStarterPicksIs32()
    {
        int teamCount = 8;
        int totalPicks = SnakeDraftCalculator.GetTotalStarterPicks(teamCount);
        Assert.Equal(32, totalPicks);

        // Turno 7 é time 7, Turno 8 é time 7 (pick duplo)
        Assert.Equal(7, SnakeDraftCalculator.GetTeamIndexForPick(7, teamCount));
        Assert.Equal(7, SnakeDraftCalculator.GetTeamIndexForPick(8, teamCount));
    }
}
