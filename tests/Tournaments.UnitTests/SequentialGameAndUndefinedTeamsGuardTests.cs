using BuildingBlocks.Domain;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Application.Common.Services;
using Tournaments.Application.Features.Matches.RecordReplayResult;
using Tournaments.Application.Features.Matches.RecordWalkover;
using Tournaments.Application.Features.Matches.UpdateGameWalkover;
using Tournaments.Application.Features.Matches.UploadMatchReplay;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Infrastructure.Repositories;
using Xunit;

namespace Tournaments.UnitTests;

public class SequentialGameAndUndefinedTeamsGuardTests
{
    private const string UndefinedTeamsMessage =
        "Não é possível registrar resultado em uma partida cujas equipes ainda não foram definidas pelos confrontos anteriores.";

    [Fact]
    public void MatchSeries_RecordGameReplay_WhenGame2BeforeGame1_ShouldThrowDomainException()
    {
        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        var ex = Assert.Throws<DomainException>(() =>
            series.RecordGameReplay(teamA, 1800, 20, 10, gameNumber: 2));

        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", ex.Message);
    }

    [Fact]
    public void MatchSeries_RecordGameReplay_WhenGame3WithOnlyGame1_ShouldThrowDomainException()
    {
        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        series.RecordGameReplay(teamA, 1800, 20, 10); // Game 1

        var ex = Assert.Throws<DomainException>(() =>
            series.RecordGameReplay(teamA, 1800, 20, 10, gameNumber: 3));

        Assert.Equal("Não é possível registrar o Jogo #3 antes de registrar o Jogo #2.", ex.Message);
    }

    [Fact]
    public void MatchSeries_UpdateGameWalkover_WhenGame2BeforeGame1_ShouldThrowDomainException()
    {
        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        var ex = Assert.Throws<DomainException>(() =>
            series.UpdateGameWalkover(2, teamA));

        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", ex.Message);
    }

    [Fact]
    public void MatchSeries_UpdateGameFromReplay_WhenGame2BeforeGame1_ShouldThrowDomainException()
    {
        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        var ex = Assert.Throws<DomainException>(() =>
            series.UpdateGameFromReplay(2, teamA, 1800, 20, 10, 1234, null));

        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", ex.Message);
    }

    [Fact]
    public async Task RecordReplayResultHandler_WhenGame2BeforeGame1_ShouldThrowDomainException()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var handler = new RecordReplayResultHandler(matchRepo, progressionService);

        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.AddAsync(series);

        var cmd = new RecordReplayResultCommand(new MatchResultIngestionDto(
            series.Id, 2, teamA, 1800, 20, 10, 1234, null));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(cmd, CancellationToken.None));

        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", ex.Message);
    }

    [Fact]
    public async Task UpdateGameWalkoverHandler_WhenGame2BeforeGame1_ShouldThrowDomainException()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var handler = new UpdateGameWalkoverHandler(matchRepo, progressionService);

        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.AddAsync(series);

        var cmd = new UpdateGameWalkoverCommand(series.Id, 2, teamA);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(cmd, CancellationToken.None));

        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", ex.Message);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenGame2BeforeGame1_ShouldThrowDomainException()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.AddAsync(series);

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, new TestDotaReplayParserService(), sender);
        using var dummyStream = new MemoryStream(new byte[64]);

        var cmd = new UploadMatchReplayCommand(series.Id, dummyStream, "match.dem", null, 2);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(cmd, CancellationToken.None));

        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", ex.Message);
    }

    [Fact]
    public void MatchSeries_WhenPendingAndTeamsNull_ShouldBlockAllRegistrationsWithUndefinedTeamsMessage()
    {
        var series = new MatchSeries(Guid.NewGuid(), "GF", SeriesFormat.Bo5);
        var teamId = Guid.NewGuid();

        // 1. RecordGameReplay
        var exReplay = Assert.Throws<DomainException>(() =>
            series.RecordGameReplay(teamId, 1800, 20, 10));
        Assert.Equal(UndefinedTeamsMessage, exReplay.Message);

        // 2. RecordWalkover
        var exWo = Assert.Throws<DomainException>(() =>
            series.RecordWalkover(teamId));
        Assert.Equal(UndefinedTeamsMessage, exWo.Message);

        // 3. UpdateGameWalkover
        var exUpdateWo = Assert.Throws<DomainException>(() =>
            series.UpdateGameWalkover(1, teamId));
        Assert.Equal(UndefinedTeamsMessage, exUpdateWo.Message);

        // 4. UpdateGameFromReplay
        var exUpdateReplay = Assert.Throws<DomainException>(() =>
            series.UpdateGameFromReplay(1, teamId, 1800, 20, 10, 1234, null));
        Assert.Equal(UndefinedTeamsMessage, exUpdateReplay.Message);
    }

    [Fact]
    public void MatchSeries_WhenOnlyOneTeamAssigned_ShouldBlockRegistrationWithUndefinedTeamsMessage()
    {
        var series = new MatchSeries(Guid.NewGuid(), "GF", SeriesFormat.Bo5);
        var teamA = Guid.NewGuid();
        series.AssignTeamA(teamA, "Team A");

        var ex = Assert.Throws<DomainException>(() =>
            series.RecordGameReplay(teamA, 1800, 20, 10));

        Assert.Equal(UndefinedTeamsMessage, ex.Message);
    }

    [Fact]
    public async Task Handlers_WhenSeriesPendingOrTeamsNull_ShouldBlockWithUndefinedTeamsMessage()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var walkoverHandler = new RecordWalkoverHandler(matchRepo, progressionService);
        var updateWalkoverHandler = new UpdateGameWalkoverHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));
        var uploadHandler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, new TestDotaReplayParserService(), sender);

        var series = new MatchSeries(Guid.NewGuid(), "GF", SeriesFormat.Bo5);
        // Sem times atribuídos (Pending)
        await matchRepo.AddAsync(series);

        var teamId = Guid.NewGuid();

        // 1. RecordReplayResultHandler
        var exReplay = await Assert.ThrowsAsync<DomainException>(() =>
            replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 1, teamId, 1800, 20, 10, 1234, null)), CancellationToken.None));
        Assert.Equal(UndefinedTeamsMessage, exReplay.Message);

        // 2. RecordWalkoverHandler
        var exWo = await Assert.ThrowsAsync<DomainException>(() =>
            walkoverHandler.Handle(new RecordWalkoverCommand(series.Id, teamId), CancellationToken.None));
        Assert.Equal(UndefinedTeamsMessage, exWo.Message);

        // 3. UpdateGameWalkoverHandler
        var exUpWo = await Assert.ThrowsAsync<DomainException>(() =>
            updateWalkoverHandler.Handle(new UpdateGameWalkoverCommand(series.Id, 1, teamId), CancellationToken.None));
        Assert.Equal(UndefinedTeamsMessage, exUpWo.Message);

        // 4. UploadMatchReplayHandler
        using var dummyStream = new MemoryStream(new byte[64]);
        var exUpload = await Assert.ThrowsAsync<DomainException>(() =>
            uploadHandler.Handle(new UploadMatchReplayCommand(series.Id, dummyStream, "test.dem", null, 1), CancellationToken.None));
        Assert.Equal(UndefinedTeamsMessage, exUpload.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MatchSeries_WhenGameNumberZeroOrNegative_ShouldThrowDomainException(int invalidGameNumber)
    {
        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        var exReplay = Assert.Throws<DomainException>(() =>
            series.RecordGameReplay(teamA, 1800, 20, 10, gameNumber: invalidGameNumber));
        Assert.Equal("O número do jogo deve ser maior que zero.", exReplay.Message);

        var exWo = Assert.Throws<DomainException>(() =>
            series.UpdateGameWalkover(invalidGameNumber, teamA));
        Assert.Equal("O número do jogo deve ser maior que zero.", exWo.Message);

        var exUpReplay = Assert.Throws<DomainException>(() =>
            series.UpdateGameFromReplay(invalidGameNumber, teamA, 1800, 20, 10, 1234, null));
        Assert.Equal("O número do jogo deve ser maior que zero.", exUpReplay.Message);
    }

    [Fact]
    public async Task Handlers_WhenAddingNewGameToCompletedSeries_ShouldThrowDomainException()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var updateWalkoverHandler = new UpdateGameWalkoverHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));
        var uploadHandler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, new TestDotaReplayParserService(), sender);

        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        // Finaliza 2-0
        series.RecordGameReplay(teamA, 1800, 20, 10);
        series.RecordGameReplay(teamA, 1800, 20, 10);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        await matchRepo.AddAsync(series);

        // 1. Tentar adicionar Jogo 3 via UpdateGameWalkoverHandler
        var exWo = await Assert.ThrowsAsync<DomainException>(() =>
            updateWalkoverHandler.Handle(new UpdateGameWalkoverCommand(series.Id, 3, teamA), CancellationToken.None));
        Assert.Contains("já foi finalizada", exWo.Message);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);

        // 2. Tentar adicionar Jogo 3 via RecordReplayResultHandler
        var exReplay = await Assert.ThrowsAsync<DomainException>(() =>
            replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 3, teamA, 1800, 20, 10, 1234, null)), CancellationToken.None));
        Assert.Contains("já foi finalizada", exReplay.Message);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);

        // 3. Tentar adicionar Jogo 3 via UploadMatchReplayHandler
        using var dummyStream = new MemoryStream(new byte[64]);
        var exUpload = await Assert.ThrowsAsync<DomainException>(() =>
            uploadHandler.Handle(new UploadMatchReplayCommand(series.Id, dummyStream, "test.dem", null, 3), CancellationToken.None));
        Assert.Contains("já foi finalizada", exUpload.Message);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
    }
}
