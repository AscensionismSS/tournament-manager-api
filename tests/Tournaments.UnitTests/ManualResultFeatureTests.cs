using BuildingBlocks.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Tournaments.Application.Common.Services;
using Tournaments.Application.Features.Matches.RecordManualResult;
using Tournaments.Application.Features.Matches.UpdateGameResult;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.ValueObjects;
using Tournaments.Infrastructure.Repositories;
using TournamentCreator.WebApi.Controllers.v1;
using TournamentCreator.WebApi.Controllers.v1.Requests;
using Xunit;

namespace Tournaments.UnitTests;

public class ManualResultFeatureTests
{
    private const string UndefinedTeamsRequiredMessage =
        "Ambas as equipes precisam estar definidas na série antes de registrar um resultado.";

    [Fact]
    public void MatchGame_CreateManual_StoresAllPropertiesCorrectly()
    {
        var seriesId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var firstPickId = Guid.NewGuid();
        var bansA = new List<int> { 1, 2, 3 };
        var bansB = new List<int> { 4, 5, 6 };

        var game = MatchGame.CreateManual(
            seriesId,
            1,
            winnerId,
            2450,
            32,
            28,
            firstPickId,
            bansA,
            bansB,
            123456789L
        );

        Assert.Equal(seriesId, game.MatchSeriesId);
        Assert.Equal(1, game.GameNumber);
        Assert.Equal(ResultSource.Manual, game.ResultSource);
        Assert.Equal(winnerId, game.WinnerTeamId);
        Assert.Equal(2450, game.DurationSeconds);
        Assert.Equal(32, game.TeamAKills);
        Assert.Equal(28, game.TeamBKills);
        Assert.False(game.IsWalkover);
        Assert.Equal(firstPickId, game.FirstPickTeamId);
        Assert.Equal(bansA, game.TeamABanHeroIds);
        Assert.Equal(bansB, game.TeamBBanHeroIds);
        Assert.Equal(123456789L, game.DotaMatchId);
        Assert.Empty(game.PlayerStats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void MatchGame_CreateManual_WhenDurationZeroOrNegative_ThrowsDomainException(long invalidDuration)
    {
        var ex = Assert.Throws<DomainException>(() =>
            MatchGame.CreateManual(Guid.NewGuid(), 1, Guid.NewGuid(), invalidDuration, 10, 10));

        Assert.Equal("A duração da partida deve ser maior que zero.", ex.Message);
    }

    [Fact]
    public void MatchGame_UpdateManual_UpdatesAllPropertiesAndClearsPlayerStats()
    {
        var seriesId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        // Inicialmente criado como replay com estatísticas de jogador
        var playerStats = new List<PlayerGameStats>
        {
            new PlayerGameStats(Guid.NewGuid(), teamA, 1, 10, 2, 5, 15000, 500, 600, null)
        };
        var game = MatchGame.CreateReplay(seriesId, 1, teamA, 1800, 20, 10, 99999L, playerStats);
        Assert.Equal(ResultSource.ReplayParser, game.ResultSource);
        Assert.NotEmpty(game.PlayerStats);

        // Atualiza manualmente para vitória do time B
        game.UpdateManual(
            teamB,
            2100,
            15,
            30,
            teamB,
            new List<int> { 10, 11 },
            new List<int> { 20, 21 },
            88888L
        );

        Assert.Equal(ResultSource.Manual, game.ResultSource);
        Assert.Equal(teamB, game.WinnerTeamId);
        Assert.Equal(2100, game.DurationSeconds);
        Assert.Equal(15, game.TeamAKills);
        Assert.Equal(30, game.TeamBKills);
        Assert.False(game.IsWalkover);
        Assert.Equal(teamB, game.FirstPickTeamId);
        Assert.Equal(2, game.TeamABanHeroIds.Count);
        Assert.Equal(2, game.TeamBBanHeroIds.Count);
        Assert.Equal(88888L, game.DotaMatchId);
        Assert.Empty(game.PlayerStats); // Estatísticas detalhadas devem ser limpas
    }

    [Fact]
    public void MatchSeries_RecordGameManual_Bo1_CompletesSeries()
    {
        var series = new MatchSeries(Guid.NewGuid(), "SF1", SeriesFormat.Bo1);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        series.RecordGameManual(teamA, 1800, 25, 15);

        Assert.Single(series.Games);
        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(0, series.ScoreTeamB);
        Assert.Equal(teamA, series.WinnerTeamId);
        Assert.Equal(teamB, series.LoserTeamId);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
    }

    [Fact]
    public void MatchSeries_RecordGameManual_Bo3_ProgressesToCompleted()
    {
        var series = new MatchSeries(Guid.NewGuid(), "SF1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        // Game 1
        series.RecordGameManual(teamA, 1800, 25, 15);
        Assert.Equal(MatchSeriesStatus.InProgress, series.Status);
        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(0, series.ScoreTeamB);
        Assert.Null(series.WinnerTeamId);

        // Game 2
        series.RecordGameManual(teamA, 2000, 30, 20);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(2, series.ScoreTeamA);
        Assert.Equal(0, series.ScoreTeamB);
        Assert.Equal(teamA, series.WinnerTeamId);
        Assert.Equal(teamB, series.LoserTeamId);
    }

    [Fact]
    public void MatchSeries_RecordGameManual_Bo2_Draw()
    {
        var series = new MatchSeries(Guid.NewGuid(), "G1", SeriesFormat.Bo2);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        series.RecordGameManual(teamA, 1800, 20, 10);
        series.RecordGameManual(teamB, 1900, 15, 25);

        Assert.Equal(2, series.Games.Count);
        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(1, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Null(series.WinnerTeamId);
        Assert.Null(series.LoserTeamId);
    }

    [Fact]
    public void MatchSeries_RecordGameManual_Guards_ThrowExpectedExceptions()
    {
        var series = new MatchSeries(Guid.NewGuid(), "SF1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var outsider = Guid.NewGuid();

        // 1. Equipes indefinidas / Pending
        var exPending = Assert.Throws<DomainException>(() =>
            series.RecordGameManual(teamA, 1800, 20, 10));
        Assert.Equal(UndefinedTeamsRequiredMessage, exPending.Message);

        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        // 2. Vencedor não participante
        var exWinner = Assert.Throws<DomainException>(() =>
            series.RecordGameManual(outsider, 1800, 20, 10));
        Assert.Equal("O time vencedor do mapa deve ser uma das equipes da série.", exWinner.Message);

        // 3. First pick inválido
        var exFp = Assert.Throws<DomainException>(() =>
            series.RecordGameManual(teamA, 1800, 20, 10, firstPickTeamId: outsider));
        Assert.Equal("O time do First Pick deve ser uma das equipes da série.", exFp.Message);

        // 4. Jogo 2 antes do Jogo 1
        var exSeq = Assert.Throws<DomainException>(() =>
            series.RecordGameManual(teamA, 1800, 20, 10, gameNumber: 2));
        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", exSeq.Message);

        // 5. Finaliza a série (2x0)
        series.RecordGameManual(teamA, 1800, 20, 10);
        series.RecordGameManual(teamA, 1900, 22, 12);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);

        // 6. Tentar adicionar novo jogo a série concluída
        var exCompleted = Assert.Throws<DomainException>(() =>
            series.RecordGameManual(teamA, 1800, 20, 10));
        Assert.Equal("A série não está disponível para receber novos jogos.", exCompleted.Message);
    }

    [Fact]
    public void MatchSeries_UpdateGame_RecalculatesScoresAndStatus_Bo3()
    {
        var series = new MatchSeries(Guid.NewGuid(), "SF1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        // Team A ganha 2-0 -> Concluída
        series.RecordGameManual(teamA, 1800, 20, 10);
        series.RecordGameManual(teamA, 1900, 22, 12);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(teamA, series.WinnerTeamId);

        // Atualiza Jogo 1 manualmente para vitória de Team B: Série volta para InProgress (1-1)
        series.UpdateGame(1, teamB, 2100, 15, 30);
        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(1, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.InProgress, series.Status);
        Assert.Null(series.WinnerTeamId);
        Assert.Null(series.LoserTeamId);

        // Atualiza Jogo 2 manualmente para vitória de Team B: Série conclui para Team B (0-2)
        series.UpdateGameManual(2, teamB, 2200, 10, 35);
        Assert.Equal(0, series.ScoreTeamA);
        Assert.Equal(2, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(teamB, series.WinnerTeamId);
        Assert.Equal(teamA, series.LoserTeamId);
    }

    [Fact]
    public void MatchSeries_UpdateGame_Guards_ThrowExpectedExceptions()
    {
        var series = new MatchSeries(Guid.NewGuid(), "SF1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var outsider = Guid.NewGuid();

        // 1. GameNumber <= 0
        var exZero = Assert.Throws<DomainException>(() =>
            series.UpdateGame(0, teamA, 1800, 20, 10));
        Assert.Equal("O número do jogo deve ser maior que zero.", exZero.Message);

        // 2. Equipes indefinidas / Pending
        var exPending = Assert.Throws<DomainException>(() =>
            series.UpdateGame(1, teamA, 1800, 20, 10));
        Assert.Equal(UndefinedTeamsRequiredMessage, exPending.Message);

        series.AssignTeams(teamA, "Team A", teamB, "Team B");

        // 3. Jogo 2 antes do Jogo 1 existir (games.Count == 0)
        var exSeq = Assert.Throws<DomainException>(() =>
            series.UpdateGame(2, teamA, 1800, 20, 10));
        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", exSeq.Message);

        // 4. Jogo 99 antes do Jogo 98
        var exSeq99 = Assert.Throws<DomainException>(() =>
            series.UpdateGame(99, teamA, 1800, 20, 10));
        Assert.Equal("Não é possível registrar o Jogo #99 antes de registrar o Jogo #98.", exSeq99.Message);

        // 5. Jogo número 1 não encontrado na série
        var exNotFound = Assert.Throws<DomainException>(() =>
            series.UpdateGame(1, teamA, 1800, 20, 10));
        Assert.Equal("Jogo número 1 não encontrado na série.", exNotFound.Message);

        // Adiciona Jogo 1
        series.RecordGameManual(teamA, 1800, 20, 10);

        // 6. Vencedor inválido
        var exWinner = Assert.Throws<DomainException>(() =>
            series.UpdateGame(1, outsider, 1800, 20, 10));
        Assert.Equal("O time vencedor do mapa deve ser uma das equipes da série.", exWinner.Message);

        // 7. First pick inválido
        var exFp = Assert.Throws<DomainException>(() =>
            series.UpdateGame(1, teamA, 1800, 20, 10, firstPickTeamId: outsider));
        Assert.Equal("O time do First Pick deve ser uma das equipes da série.", exFp.Message);
    }

    [Fact]
    public async Task RecordManualResultHandler_ExecutesSuccessfullyAndProgressesBracket()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var handler = new RecordManualResultHandler(matchRepo, progressionService);

        var tournament = new Tournament("Manual Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo1).Value!);
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var seriesSF1 = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo1);
        seriesSF1.AssignTeams(teamA, "Alpha", teamB, "Beta");
        await matchRepo.AddAsync(seriesSF1);

        var seriesGF = new MatchSeries(tournament.Id, "GF", SeriesFormat.Bo1);
        await matchRepo.AddAsync(seriesGF);

        var cmd = new RecordManualResultCommand(
            seriesSF1.Id,
            teamA,
            1850,
            28,
            14,
            teamA,
            new List<int> { 1, 2, 3 },
            new List<int> { 4, 5, 6 },
            777123L
        );

        await handler.Handle(cmd, CancellationToken.None);

        var updatedSF1 = await matchRepo.GetByIdAsync(seriesSF1.Id);
        Assert.NotNull(updatedSF1);
        Assert.Equal(MatchSeriesStatus.Completed, updatedSF1.Status);
        Assert.Equal(1, updatedSF1.ScoreTeamA);
        Assert.Equal(teamA, updatedSF1.WinnerTeamId);

        var game = updatedSF1.Games.First();
        Assert.Equal(ResultSource.Manual, game.ResultSource);
        Assert.Equal(1850, game.DurationSeconds);
        Assert.Equal(777123L, game.DotaMatchId);

        // Verifica que o vencedor avançou para a Grande Final (GF)
        var updatedGF = await matchRepo.GetByIdAsync(seriesGF.Id);
        Assert.NotNull(updatedGF);
        Assert.Equal(teamA, updatedGF.TeamAId);
        Assert.Equal("Alpha", updatedGF.TeamAName);
    }

    [Fact]
    public async Task RecordManualResultHandler_Guards_ThrowDomainException()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var handler = new RecordManualResultHandler(matchRepo, progressionService);

        var series = new MatchSeries(Guid.NewGuid(), "SF1", SeriesFormat.Bo3);
        // Sem times atribuídos (Pending)
        await matchRepo.AddAsync(series);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        // 1. Equipes indefinidas
        var exPending = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new RecordManualResultCommand(series.Id, teamA, 1800, 20, 10), CancellationToken.None));
        Assert.Equal(UndefinedTeamsRequiredMessage, exPending.Message);

        series.AssignTeams(teamA, "Team A", teamB, "Team B");
        await matchRepo.UpdateAsync(series);

        // 2. Sequencial inválido: Game 2 antes de Game 1
        var exSeq = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new RecordManualResultCommand(series.Id, teamA, 1800, 20, 10, GameNumber: 2), CancellationToken.None));
        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", exSeq.Message);

        // Conclui a série
        series.RecordGameManual(teamA, 1800, 20, 10);
        series.RecordGameManual(teamA, 1900, 25, 15);
        await matchRepo.UpdateAsync(series);

        // 3. Série já finalizada
        var exCompleted = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new RecordManualResultCommand(series.Id, teamA, 1800, 20, 10), CancellationToken.None));
        Assert.Contains("já foi finalizada", exCompleted.Message);
    }

    [Fact]
    public async Task UpdateGameResultHandler_WhenSeriesCompleted_UndoesProgressionAndReProgresses()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var updateHandler = new UpdateGameResultHandler(matchRepo, progressionService);

        var tournament = new Tournament("Edit Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo1).Value!);
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var seriesSF1 = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo1);
        seriesSF1.AssignTeams(teamA, "Alpha", teamB, "Beta");
        seriesSF1.RecordGameManual(teamA, 1800, 20, 10); // Alpha ganhou
        await matchRepo.AddAsync(seriesSF1);

        // Alpha avançou para GF
        await progressionService.ProgressBracketAsync(seriesSF1);

        var seriesGF = new MatchSeries(tournament.Id, "GF", SeriesFormat.Bo1);
        seriesGF.AssignTeamA(teamA, "Alpha");
        await matchRepo.AddAsync(seriesGF);

        // Admin edita o Jogo 1 manualmente definindo Beta como vencedor
        var cmd = new UpdateGameResultCommand(
            seriesSF1.Id,
            1,
            teamB,
            2200,
            12,
            30,
            teamB,
            new List<int> { 5, 6 },
            new List<int> { 7, 8 },
            999000L
        );

        await updateHandler.Handle(cmd, CancellationToken.None);

        var updatedSF1 = await matchRepo.GetByIdAsync(seriesSF1.Id);
        Assert.NotNull(updatedSF1);
        Assert.Equal(teamB, updatedSF1.WinnerTeamId);
        Assert.Equal(0, updatedSF1.ScoreTeamA);
        Assert.Equal(1, updatedSF1.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, updatedSF1.Status);

        // Slot downstream da GF agora contém Beta!
        var updatedGF = await matchRepo.GetByIdAsync(seriesGF.Id);
        Assert.NotNull(updatedGF);
        Assert.Equal(teamB, updatedGF.TeamAId);
        Assert.Equal("Beta", updatedGF.TeamAName);
    }

    [Fact]
    public async Task UpdateGameResultHandler_Guards_ThrowDomainException()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var handler = new UpdateGameResultHandler(matchRepo, progressionService);

        var series = new MatchSeries(Guid.NewGuid(), "SF1", SeriesFormat.Bo3);
        await matchRepo.AddAsync(series);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        // 1. Pending / times indefinidos
        var exPending = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new UpdateGameResultCommand(series.Id, 1, teamA, 1800, 20, 10), CancellationToken.None));
        Assert.Equal(UndefinedTeamsRequiredMessage, exPending.Message);

        series.AssignTeams(teamA, "Alpha", teamB, "Beta");
        await matchRepo.UpdateAsync(series);

        // 2. GameNumber <= 0
        var exZero = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new UpdateGameResultCommand(series.Id, 0, teamA, 1800, 20, 10), CancellationToken.None));
        Assert.Equal("O número do jogo deve ser maior que zero.", exZero.Message);

        // 3. Jogo 2 antes do Jogo 1 existir
        var exSeq = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new UpdateGameResultCommand(series.Id, 2, teamA, 1800, 20, 10), CancellationToken.None));
        Assert.Equal("Não é possível registrar o Jogo #2 antes de registrar o Jogo #1.", exSeq.Message);

        // 4. Jogo 1 não encontrado
        var exNotFound = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new UpdateGameResultCommand(series.Id, 1, teamA, 1800, 20, 10), CancellationToken.None));
        Assert.Equal("Jogo número 1 não encontrado na série.", exNotFound.Message);
    }

    [Fact]
    public void Validators_ValidateCommandsCorrectly()
    {
        var recordValidator = new RecordManualResultValidator();
        var updateValidator = new UpdateGameResultValidator();

        // Inválidos
        var invalidRecord = new RecordManualResultCommand(Guid.Empty, Guid.Empty, 0, -1, -1);
        var recordRes = recordValidator.Validate(invalidRecord);
        Assert.False(recordRes.IsValid);
        Assert.Contains(recordRes.Errors, e => e.PropertyName == "MatchSeriesId");
        Assert.Contains(recordRes.Errors, e => e.PropertyName == "WinnerTeamId");
        Assert.Contains(recordRes.Errors, e => e.PropertyName == "DurationSeconds");
        Assert.Contains(recordRes.Errors, e => e.PropertyName == "TeamAKills");
        Assert.Contains(recordRes.Errors, e => e.PropertyName == "TeamBKills");

        var invalidUpdate = new UpdateGameResultCommand(Guid.Empty, 0, Guid.Empty, 0, -1, -1);
        var updateRes = updateValidator.Validate(invalidUpdate);
        Assert.False(updateRes.IsValid);
        Assert.Contains(updateRes.Errors, e => e.PropertyName == "MatchSeriesId");
        Assert.Contains(updateRes.Errors, e => e.PropertyName == "GameNumber");
        Assert.Contains(updateRes.Errors, e => e.PropertyName == "WinnerTeamId");
        Assert.Contains(updateRes.Errors, e => e.PropertyName == "DurationSeconds");
        Assert.Contains(updateRes.Errors, e => e.PropertyName == "TeamAKills");
        Assert.Contains(updateRes.Errors, e => e.PropertyName == "TeamBKills");

        // Válidos
        var validRecord = new RecordManualResultCommand(Guid.NewGuid(), Guid.NewGuid(), 1800, 10, 15);
        Assert.True(recordValidator.Validate(validRecord).IsValid);

        var validUpdate = new UpdateGameResultCommand(Guid.NewGuid(), 1, Guid.NewGuid(), 1800, 10, 15);
        Assert.True(updateValidator.Validate(validUpdate).IsValid);
    }

    [Fact]
    public async Task MatchesController_Endpoints_InvokeMediatrAndReturnOk()
    {
        var senderMock = new TestSender(req => Task.CompletedTask);
        var controller = new MatchesController(senderMock);

        var matchId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();

        // 1. POST /api/v1/matches/{id}/result/manual
        var recordReq = new RecordManualResultRequest(
            winnerId,
            1800,
            25,
            20,
            winnerId,
            new List<int> { 1, 2 },
            new List<int> { 3, 4 },
            12345L
        );
        var recordResult = await controller.RecordManualResult(matchId, recordReq, CancellationToken.None);
        var recordOk = Assert.IsType<OkObjectResult>(recordResult);
        Assert.NotNull(recordOk.Value);

        // 2. PUT /api/v1/matches/{id}/games/{gameNumber}
        var updateReq = new UpdateGameResultRequest(
            winnerId,
            1900,
            22,
            18,
            winnerId,
            new List<int> { 5, 6 },
            new List<int> { 7, 8 },
            54321L
        );
        var updateResult = await controller.UpdateGameResult(matchId, 1, updateReq, CancellationToken.None);
        var updateOk = Assert.IsType<OkObjectResult>(updateResult);
        Assert.NotNull(updateOk.Value);
    }

    [Fact]
    public async Task RecordManualResultHandler_WhenGameNumberMatchesExistingGame_UpdatesInPlaceAndReProgresses()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var handler = new RecordManualResultHandler(matchRepo, progressionService);

        var tournament = new Tournament("Re-Record Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo1).Value!);
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var seriesSF1 = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo1);
        seriesSF1.AssignTeams(teamA, "Alpha", teamB, "Beta");
        seriesSF1.RecordGameManual(teamA, 1800, 20, 10); // Alpha ganha
        await matchRepo.AddAsync(seriesSF1);

        // Alpha avançou para GF
        await progressionService.ProgressBracketAsync(seriesSF1);

        var seriesGF = new MatchSeries(tournament.Id, "GF", SeriesFormat.Bo1);
        seriesGF.AssignTeamA(teamA, "Alpha");
        await matchRepo.AddAsync(seriesGF);

        // Reenvia Game 1 via RecordManualResultHandler especificando GameNumber = 1 com Beta como vencedor
        var cmd = new RecordManualResultCommand(
            seriesSF1.Id,
            teamB,
            2400,
            10,
            30,
            FirstPickTeamId: Guid.Empty, // Deve normalizar para null
            GameNumber: 1
        );

        await handler.Handle(cmd, CancellationToken.None);

        var updatedSF1 = await matchRepo.GetByIdAsync(seriesSF1.Id);
        Assert.NotNull(updatedSF1);
        Assert.Single(updatedSF1.Games);
        Assert.Equal(teamB, updatedSF1.WinnerTeamId);
        Assert.Equal(0, updatedSF1.ScoreTeamA);
        Assert.Equal(1, updatedSF1.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, updatedSF1.Status);
        Assert.Null(updatedSF1.Games.First().FirstPickTeamId); // Normalizado de Guid.Empty

        // Downstream GF agora contém Beta!
        var updatedGF = await matchRepo.GetByIdAsync(seriesGF.Id);
        Assert.NotNull(updatedGF);
        Assert.Equal(teamB, updatedGF.TeamAId);
        Assert.Equal("Beta", updatedGF.TeamAName);
    }

    [Fact]
    public async Task UpdateGameResultHandler_WhenDownstreamMatchAlreadyHasGames_ThrowsDomainExceptionAndDoesNotCorrupt()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var handler = new UpdateGameResultHandler(matchRepo, progressionService);

        var tournament = new Tournament("Downstream Guard Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo1).Value!);
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var teamC = Guid.NewGuid();

        var seriesSF1 = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo1);
        seriesSF1.AssignTeams(teamA, "Alpha", teamB, "Beta");
        seriesSF1.RecordGameManual(teamA, 1800, 20, 10); // Alpha ganha
        await matchRepo.AddAsync(seriesSF1);

        // Alpha avançou para GF
        await progressionService.ProgressBracketAsync(seriesSF1);

        var seriesGF = new MatchSeries(tournament.Id, "GF", SeriesFormat.Bo1);
        seriesGF.AssignTeams(teamA, "Alpha", teamC, "Gamma");
        // A Grande Final já jogou um mapa!
        seriesGF.RecordGameManual(teamA, 1500, 15, 5);
        await matchRepo.AddAsync(seriesGF);

        // Tentar editar SF1 deve falhar porque GF já tem jogos
        var cmd = new UpdateGameResultCommand(seriesSF1.Id, 1, teamB, 2000, 10, 25);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(cmd, CancellationToken.None));

        Assert.Contains("pois a partida subsequente 'GF' já possui mapas jogados", ex.Message);

        // O resultado da SF1 e o downstream GF continuam intactos
        var sf1 = await matchRepo.GetByIdAsync(seriesSF1.Id);
        Assert.Equal(teamA, sf1!.WinnerTeamId);

        var gf = await matchRepo.GetByIdAsync(seriesGF.Id);
        Assert.Equal(teamA, gf!.TeamAId);
    }

    [Fact]
    public async Task Handlers_WhenWinnerIdNotParticipating_ThrowsDomainExceptionBeforeSideEffects()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var recordHandler = new RecordManualResultHandler(matchRepo, progressionService);
        var updateHandler = new UpdateGameResultHandler(matchRepo, progressionService);

        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        series.AssignTeams(teamA, "Alpha", teamB, "Beta");
        series.RecordGameManual(teamA, 1800, 20, 10);
        await matchRepo.AddAsync(series);

        // 1. RecordManualResultHandler com vencedor estranho
        var exRecord = await Assert.ThrowsAsync<DomainException>(() =>
            recordHandler.Handle(new RecordManualResultCommand(series.Id, outsider, 1800, 20, 10), CancellationToken.None));
        Assert.Equal("O time vencedor do mapa deve ser uma das equipes da série.", exRecord.Message);

        // 2. UpdateGameResultHandler com vencedor estranho
        var exUpdate = await Assert.ThrowsAsync<DomainException>(() =>
            updateHandler.Handle(new UpdateGameResultCommand(series.Id, 1, outsider, 1800, 20, 10), CancellationToken.None));
        Assert.Equal("O time vencedor do mapa deve ser uma das equipes da série.", exUpdate.Message);
    }
}

