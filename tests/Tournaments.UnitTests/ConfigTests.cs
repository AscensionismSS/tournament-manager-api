using Tournaments.Domain.Enums;
using Tournaments.Domain.ValueObjects;
using Xunit;

namespace Tournaments.UnitTests;

public class ConfigTests
{
    [Theory]
    [InlineData(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo1)] // Torneio relâmpago tudo MD1
    [InlineData(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo3)]
    [InlineData(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo3)]
    [InlineData(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5)]
    [InlineData(SeriesFormat.Bo2, SeriesFormat.Bo3, SeriesFormat.Bo3)]
    [InlineData(SeriesFormat.Bo2, SeriesFormat.Bo3, SeriesFormat.Bo5)]
    [InlineData(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo3)]
    [InlineData(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo5)]
    [InlineData(SeriesFormat.Bo3, SeriesFormat.Bo5, SeriesFormat.Bo5)]
    public void SeriesConfig_ValidCombinations_ShouldSucceed(
        SeriesFormat groupFormat,
        SeriesFormat playoffFormat,
        SeriesFormat finalFormat)
    {
        var result = SeriesConfig.Create(groupFormat, playoffFormat, finalFormat);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(groupFormat, result.Value.GroupStageFormat);
        Assert.Equal(playoffFormat, result.Value.PlayoffFormat);
        Assert.Equal(finalFormat, result.Value.GrandFinalFormat);
    }

    [Fact]
    public void SeriesConfig_PlayoffBo1ToFinalBo5_ShouldFail_DueToDisproportionateJump()
    {
        var result = SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo5);

        Assert.True(result.IsFailure);
        Assert.Contains("não pode pular direto para MD5", result.Error);
    }

    [Theory]
    [InlineData(SeriesFormat.Bo1)]
    [InlineData(SeriesFormat.Bo2)]
    [InlineData(null)]
    public void SeriesConfig_PlayoffBo5_ShouldFail_WhenGroupStageIsNotBo3(SeriesFormat? groupFormat)
    {
        var result = SeriesConfig.Create(groupFormat, SeriesFormat.Bo5, SeriesFormat.Bo5);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void SeriesConfig_GroupBo3ToPlayoffBo1_ShouldFail_DueToRegression()
    {
        var result = SeriesConfig.Create(SeriesFormat.Bo3, SeriesFormat.Bo1, SeriesFormat.Bo1);

        Assert.True(result.IsFailure);
        Assert.Contains("não podem ter formato inferior", result.Error);
    }

    [Fact]
    public void SeriesConfig_PlayoffBo2_ShouldFail_BecausePlayoffsNeedDecisiveWinner()
    {
        var result = SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo2, SeriesFormat.Bo3);

        Assert.True(result.IsFailure);
        Assert.Contains("Playoffs não podem ter formato Bo2", result.Error);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(5, false)] // No BYE allowed
    [InlineData(6, false)]
    [InlineData(7, false)]
    public void TournamentFormatConfig_DirectPlayoffs_OnlyAllows4Or8Teams(int teamCount, bool shouldSucceed)
    {
        var result = TournamentFormatConfig.Create(
            TournamentType.DirectPlayoffs,
            PlayoffFormat.SingleElimination,
            teamCount);

        Assert.Equal(shouldSucceed, result.IsSuccess);
        if (!shouldSucceed)
        {
            Assert.Contains("exige exatamente 4 ou 8 equipes", result.Error);
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void TournamentFormatConfig_GroupStage_AllowsAllCountsFrom4To8(int teamCount)
    {
        var result = TournamentFormatConfig.Create(
            TournamentType.GroupStageAndPlayoffs,
            PlayoffFormat.DoubleElimination,
            teamCount);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TournamentFormatConfig_TwoGroups_OnlyAllowedFor8Teams()
    {
        var invalidResult = TournamentFormatConfig.Create(
            TournamentType.GroupStageAndPlayoffs,
            PlayoffFormat.DoubleElimination,
            6,
            groupCount: 2);

        var validResult = TournamentFormatConfig.Create(
            TournamentType.GroupStageAndPlayoffs,
            PlayoffFormat.DoubleElimination,
            8,
            groupCount: 2);

        Assert.True(invalidResult.IsFailure);
        Assert.True(validResult.IsSuccess);
    }
}
