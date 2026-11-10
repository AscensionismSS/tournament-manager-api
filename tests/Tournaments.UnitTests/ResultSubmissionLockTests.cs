using BuildingBlocks.Domain;
using Tournaments.Application.Common.Services;
using Tournaments.Application.Features.Matches.RecordManualResult;
using Tournaments.Application.Features.Matches.RecordReplayResult;
using Tournaments.Application.Features.Matches.RecordWalkover;
using Tournaments.Application.Features.Matches.RemoveLastGame;
using Tournaments.Application.Features.Matches.RollbackMatchSeries;
using Tournaments.Application.Features.Matches.UpdateGameResult;
using Tournaments.Application.Features.Matches.UpdateGameWalkover;
using Tournaments.Application.Features.Matches.UploadMatchReplay;
using Tournaments.Application.Features.Tournaments;
using Tournaments.Application.Features.Tournaments.AdvanceToPlayoffs;
using Tournaments.Application.Features.Tournaments.LockResultSubmission;
using Tournaments.Application.Features.Tournaments.RevertToGroupStage;
using Tournaments.Application.Features.Tournaments.UnlockResultSubmission;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.ValueObjects;
using Tournaments.Infrastructure.Repositories;
using Xunit;

namespace Tournaments.UnitTests;

public class ResultSubmissionLockTests
{
    private static Tournament CreateTestTournament()
    {
        return new Tournament(
            "Lock Test Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo3).Value!);
    }

    [Fact]
    public void Tournament_LockResultSubmission_ShouldSetPropertiesAndIncrementVersion()
    {
        var tournament = CreateTestTournament();
        var initialVersion = tournament.Version;

        Assert.False(tournament.IsResultSubmissionLocked);
        Assert.Null(tournament.ResultLockReason);
        Assert.Null(tournament.ResultLockedAtUtc);
        Assert.Null(tournament.ResultLockedBy);

        tournament.LockResultSubmission("Manutenção dos servidores", "admin-123");

        Assert.True(tournament.IsResultSubmissionLocked);
        Assert.Equal("Manutenção dos servidores", tournament.ResultLockReason);
        Assert.NotNull(tournament.ResultLockedAtUtc);
        Assert.Equal("admin-123", tournament.ResultLockedBy);
        Assert.True(tournament.Version > initialVersion);
    }

    [Fact]
    public void Tournament_LockResultSubmission_WhenAlreadyLocked_ShouldThrowDomainException()
    {
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Motivo 1", "admin-1");

        var ex = Assert.Throws<DomainException>(() => tournament.LockResultSubmission("Motivo 2", "admin-2"));
        Assert.Contains("já está bloqueado", ex.Message);
    }

    [Fact]
    public void Tournament_LockResultSubmission_WithEmptyAdmin_ShouldThrowDomainException()
    {
        var tournament = CreateTestTournament();

        Assert.Throws<DomainException>(() => tournament.LockResultSubmission("Motivo", ""));
        Assert.Throws<DomainException>(() => tournament.LockResultSubmission("Motivo", "   "));
    }

    [Fact]
    public void Tournament_UnlockResultSubmission_ShouldClearPropertiesAndIncrementVersion()
    {
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Bloqueio", "admin-1");
        var versionBeforeUnlock = tournament.Version;

        tournament.UnlockResultSubmission("admin-2");

        Assert.False(tournament.IsResultSubmissionLocked);
        Assert.Null(tournament.ResultLockReason);
        Assert.Null(tournament.ResultLockedAtUtc);
        Assert.Null(tournament.ResultLockedBy);
        Assert.True(tournament.Version > versionBeforeUnlock);
    }

    [Fact]
    public void Tournament_UnlockResultSubmission_WhenNotLocked_ShouldThrowDomainException()
    {
        var tournament = CreateTestTournament();

        var ex = Assert.Throws<DomainException>(() => tournament.UnlockResultSubmission("admin-1"));
        Assert.Contains("não está bloqueado", ex.Message);
    }

    [Fact]
    public void Tournament_UnlockResultSubmission_WithEmptyAdmin_ShouldThrowDomainException()
    {
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Bloqueio", "admin-1");

        Assert.Throws<DomainException>(() => tournament.UnlockResultSubmission(""));
        Assert.Throws<DomainException>(() => tournament.UnlockResultSubmission("   "));
    }

    [Fact]
    public void TournamentMapper_ToDto_ShouldExposeLockProperties()
    {
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Auditoria de resultados", "admin-root");

        var dto = tournament.ToDto();

        Assert.True(dto.IsResultSubmissionLocked);
        Assert.Equal("Auditoria de resultados", dto.ResultLockReason);
        Assert.NotNull(dto.ResultLockedAtUtc);
    }

    [Fact]
    public async Task LockAndUnlockResultSubmissionHandlers_ShouldWorkCorrectly()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var tournament = CreateTestTournament();
        await tournamentRepo.AddAsync(tournament);

        var lockHandler = new LockResultSubmissionHandler(tournamentRepo);
        var lockResult = await lockHandler.Handle(new LockResultSubmissionCommand(tournament.Id, "Auditoria", "admin-1"), CancellationToken.None);

        Assert.True(lockResult.IsResultSubmissionLocked);
        Assert.Equal("Auditoria", lockResult.ResultLockReason);

        var unlockHandler = new UnlockResultSubmissionHandler(tournamentRepo);
        var unlockResult = await unlockHandler.Handle(new UnlockResultSubmissionCommand(tournament.Id, "admin-1"), CancellationToken.None);

        Assert.False(unlockResult.IsResultSubmissionLocked);
        Assert.Null(unlockResult.ResultLockReason);
    }

    [Fact]
    public void ReplayValidationService_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var validator = new ReplayValidationService();
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Investigação anti-cheat", "admin-1");

        var series = new MatchSeries(tournament.Id, "M1", SeriesFormat.Bo3);
        series.AssignTeams(Guid.NewGuid(), "Team A", Guid.NewGuid(), "Team B");

        var ex = Assert.Throws<DomainException>(() => validator.ValidateSeriesAndTournamentState(series, tournament, 1));
        Assert.Contains("bloqueado pela administração", ex.Message);
        Assert.Contains("Investigação anti-cheat", ex.Message);
    }

    [Fact]
    public async Task RecordManualResultHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Travado", "admin");
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var series = new MatchSeries(tournament.Id, "M1", SeriesFormat.Bo3);
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.AddAsync(series);

        var handler = new RecordManualResultHandler(matchRepo, tournamentRepo);
        var cmd = new RecordManualResultCommand(series.Id, teamA, 1800, 30, 20);

        var ex = Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        var res = await ex;
        Assert.Contains("bloqueado pela administração", res.Message);
    }

    [Fact]
    public async Task RecordWalkoverHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Travado", "admin");
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var series = new MatchSeries(tournament.Id, "M1", SeriesFormat.Bo3);
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.AddAsync(series);

        var handler = new RecordWalkoverHandler(matchRepo, tournamentRepo);
        var cmd = new RecordWalkoverCommand(series.Id, teamA);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("bloqueado pela administração", ex.Message);
    }

    [Fact]
    public async Task UpdateGameResultHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Travado", "admin");
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var series = new MatchSeries(tournament.Id, "M1", SeriesFormat.Bo3);
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.AddAsync(series);

        var handler = new UpdateGameResultHandler(matchRepo, tournamentRepo);
        var cmd = new UpdateGameResultCommand(series.Id, 1, teamA, 1800, 20, 10);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("bloqueado pela administração", ex.Message);
    }

    [Fact]
    public async Task UpdateGameWalkoverHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Travado", "admin");
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var series = new MatchSeries(tournament.Id, "M1", SeriesFormat.Bo3);
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.AddAsync(series);

        var handler = new UpdateGameWalkoverHandler(matchRepo, tournamentRepo);
        var cmd = new UpdateGameWalkoverCommand(series.Id, 1, teamA);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("bloqueado pela administração", ex.Message);
    }

    [Fact]
    public async Task RemoveLastGameHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Travado", "admin");
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var series = new MatchSeries(tournament.Id, "M1", SeriesFormat.Bo3);
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        series.RecordGameManual(teamA, 1500, 20, 10);
        await matchRepo.AddAsync(series);

        var handler = new RemoveLastGameHandler(matchRepo, progressionService, tournamentRepo);
        var cmd = new RemoveLastGameCommand(series.Id);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("bloqueado pela administração", ex.Message);
    }

    [Fact]
    public async Task RollbackMatchSeriesHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Travado", "admin");
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var series = new MatchSeries(tournament.Id, "M1", SeriesFormat.Bo3);
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        series.RecordWalkover(teamA);
        await matchRepo.AddAsync(series);

        var handler = new RollbackMatchSeriesHandler(matchRepo, progressionService, tournamentRepo);
        var cmd = new RollbackMatchSeriesCommand(series.Id);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("bloqueado pela administração", ex.Message);
    }

    [Fact]
    public async Task RecordReplayResultHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournament = CreateTestTournament();
        tournament.LockResultSubmission("Travado", "admin");
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var series = new MatchSeries(tournament.Id, "M1", SeriesFormat.Bo3);
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.AddAsync(series);

        var handler = new RecordReplayResultHandler(matchRepo, tournamentRepo);
        var cmd = new RecordReplayResultCommand(new MatchResultIngestionDto(
            series.Id,
            1,
            teamA,
            2400,
            35,
            20,
            1234567,
            []
        ));

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("bloqueado pela administração", ex.Message);
    }

    [Fact]
    public void Tournament_LockResultSubmission_ShouldTruncateReason_WhenLongerThan500Chars()
    {
        var tournament = CreateTestTournament();
        var excessivelyLongReason = new string('A', 600);

        tournament.LockResultSubmission(excessivelyLongReason, "admin-1");

        Assert.NotNull(tournament.ResultLockReason);
        Assert.Equal(500, tournament.ResultLockReason.Length);
        Assert.True(tournament.IsResultSubmissionLocked);
    }

    [Fact]
    public void ReplayValidationService_ValidateCallerPermission_WhenNotAdminAndUserIdNull_ShouldThrowDomainException()
    {
        var validator = new ReplayValidationService();
        var teamA = new DraftTeam(Guid.NewGuid(), "Captain A");
        var teamB = new DraftTeam(Guid.NewGuid(), "Captain B");

        var ex = Assert.Throws<DomainException>(() =>
            validator.ValidateCallerPermission(null, teamA, teamB, isAdmin: false));

        Assert.Contains("Apenas os capitães", ex.Message);
    }

    [Fact]
    public void ReplayValidationService_ValidateCallerPermission_WhenCaptainMatchedViaExternalUserId_ShouldSucceed()
    {
        var validator = new ReplayValidationService();
        var authUserId = Guid.NewGuid();

        // Time A cujo capitão possui Player.Id diferente mas ExternalUserId igual ao authUserId do JWT
        var teamA = new DraftTeam(Guid.NewGuid(), "Captain A", authUserId.ToString());
        var teamB = new DraftTeam(Guid.NewGuid(), "Captain B");

        // Não lança exceção: o capitão é reconhecido pelo ExternalUserId
        validator.ValidateCallerPermission(authUserId, teamA, teamB, isAdmin: false);
    }

    [Fact]
    public async Task RevertToGroupStageHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();

        var tournament = new Tournament(
            "Locked Group Cup",
            TournamentFormatConfig.Create(TournamentType.GroupStageAndPlayoffs, PlayoffFormat.SingleElimination, 4, 1).Value!,
            SeriesConfig.Create(SeriesFormat.Bo2, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        for (int i = 1; i <= 4; i++)
        {
            tournament.AddTeam(Guid.NewGuid(), $"Team {i}");
        }

        tournament.Start();
        tournament.GeneratePlayoffs();
        tournament.LockResultSubmission("Resultados sob investigação", "admin-root");
        await tournamentRepo.AddAsync(tournament);

        var handler = new RevertToGroupStageHandler(tournamentRepo, matchRepo);
        var cmd = new RevertToGroupStageCommand(tournament.Id);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("bloqueado pela administração", ex.Message);
    }

    [Fact]
    public async Task AdvanceToPlayoffsHandler_WhenTournamentLocked_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();

        var tournament = new Tournament(
            "Locked Playoffs Cup",
            TournamentFormatConfig.Create(TournamentType.GroupStageAndPlayoffs, PlayoffFormat.SingleElimination, 4, 1).Value!,
            SeriesConfig.Create(SeriesFormat.Bo2, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        for (int i = 1; i <= 4; i++)
        {
            tournament.AddTeam(Guid.NewGuid(), $"Team {i}");
        }

        tournament.Start();
        tournament.LockResultSubmission("Resultados sob investigação", "admin-root");
        await tournamentRepo.AddAsync(tournament);

        var handler = new AdvanceToPlayoffsHandler(tournamentRepo, matchRepo);
        var cmd = new AdvanceToPlayoffsCommand(tournament.Id);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("bloqueado pela administração", ex.Message);
    }
}
