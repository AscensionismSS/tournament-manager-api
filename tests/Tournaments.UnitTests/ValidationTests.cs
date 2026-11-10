using Tournaments.Application.Features.Draft.PickStarterPlayer;
using Tournaments.Application.Features.Matches.RecordReplayResult;
using Tournaments.Application.Features.Matches.RecordWalkover;
using Tournaments.Application.Features.Tournaments.CreateTournament;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Enums;
using Xunit;

namespace Tournaments.UnitTests;

public class ValidationTests
{
    [Fact]
    public void CreateTournamentValidator_ShouldFail_WhenNameIsEmptyOrTeamCountOutOfRange()
    {
        var validator = new CreateTournamentValidator();

        var invalidCommand = new CreateTournamentCommand(
            Name: "",
            Type: TournamentType.DirectPlayoffs,
            PlayoffFormat: PlayoffFormat.SingleElimination,
            TeamCount: 3, // Invalid (< 4)
            GroupCount: 1,
            GroupStageFormat: null,
            PlayoffSeriesFormat: SeriesFormat.Bo3,
            GrandFinalSeriesFormat: SeriesFormat.Bo5
        );

        var result = validator.Validate(invalidCommand);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTournamentCommand.Name));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTournamentCommand.TeamCount));
    }

    [Fact]
    public void CreateTournamentValidator_ShouldPass_WhenCommandIsValid()
    {
        var validator = new CreateTournamentValidator();

        var validCommand = new CreateTournamentCommand(
            Name: "TI 2026 In-House",
            Type: TournamentType.GroupStageAndPlayoffs,
            PlayoffFormat: PlayoffFormat.DoubleElimination,
            TeamCount: 8,
            GroupCount: 2,
            GroupStageFormat: SeriesFormat.Bo2,
            PlayoffSeriesFormat: SeriesFormat.Bo3,
            GrandFinalSeriesFormat: SeriesFormat.Bo5
        );

        var result = validator.Validate(validCommand);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void PickStarterPlayerValidator_ShouldFail_WhenFieldsEmptyOrNegativeVersion()
    {
        var validator = new PickStarterPlayerValidator();

        var invalidCommand = new PickStarterPlayerCommand(Guid.Empty, Guid.Empty, -1);
        var result = validator.Validate(invalidCommand);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PickStarterPlayerCommand.DraftSessionId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PickStarterPlayerCommand.PlayerId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PickStarterPlayerCommand.ExpectedVersion));
    }

    [Fact]
    public void RecordReplayResultValidator_ShouldFail_WhenDurationOrWinnerInvalid()
    {
        var validator = new RecordReplayResultValidator();

        var invalidCommand = new RecordReplayResultCommand(
            new MatchResultIngestionDto(
                MatchSeriesId: Guid.Empty,
                GameNumber: 0,
                WinnerTeamId: Guid.Empty,
                DurationSeconds: 0,
                TeamAKills: 0,
                TeamBKills: 0,
                DotaMatchId: null,
                PlayerStats: null
            )
        );

        var result = validator.Validate(invalidCommand);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains(nameof(MatchResultIngestionDto.DurationSeconds)));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains(nameof(MatchResultIngestionDto.GameNumber)));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains(nameof(MatchResultIngestionDto.MatchSeriesId)));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains(nameof(MatchResultIngestionDto.WinnerTeamId)));
    }

    [Fact]
    public void RecordWalkoverValidator_ShouldFail_WhenIdsEmpty()
    {
        var validator = new RecordWalkoverValidator();
        var invalidCommand = new RecordWalkoverCommand(Guid.Empty, Guid.Empty);

        var result = validator.Validate(invalidCommand);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RecordWalkoverCommand.MatchSeriesId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RecordWalkoverCommand.WinnerTeamId));
    }
}
