using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Application.Common.Services;
using Tournaments.Application.Features.Matches.RecordReplayResult;
using Tournaments.Application.Features.Matches.UpdateGameWalkover;
using Tournaments.Application.Features.Matches.UploadMatchReplay;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.ValueObjects;
using Tournaments.Infrastructure.Repositories;
using Xunit;

namespace Tournaments.UnitTests;

public class MatchGameEditReplaceTests
{
    [Fact]
    public void MatchGame_UpdateFromReplay_ShouldUpdateAllPropertiesAndResetState()
    {
        var seriesId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var game = MatchGame.CreateWalkover(seriesId, 1, teamA);
        Assert.True(game.IsWalkover);
        Assert.Equal(ResultSource.Walkover, game.ResultSource);
        Assert.Equal(0, game.DurationSeconds);

        var playerStats = new List<PlayerGameStats>
        {
            new PlayerGameStats(Guid.NewGuid(), teamB, 1, 10, 2, 8, 20000, 600, 700, new List<int> { 1, 2, 3 })
        };

        game.UpdateFromReplay(
            teamB,
            2400,
            25,
            35,
            999888777,
            playerStats,
            teamB,
            new List<int> { 10, 11 },
            new List<int> { 20, 21 }
        );

        Assert.False(game.IsWalkover);
        Assert.Equal(ResultSource.ReplayParser, game.ResultSource);
        Assert.Equal(teamB, game.WinnerTeamId);
        Assert.Equal(2400, game.DurationSeconds);
        Assert.Equal(25, game.TeamAKills);
        Assert.Equal(35, game.TeamBKills);
        Assert.Equal(999888777, game.DotaMatchId);
        Assert.Equal(teamB, game.FirstPickTeamId);
        Assert.Single(game.PlayerStats);
        Assert.Equal(2, game.TeamABanHeroIds.Count);
        Assert.Equal(2, game.TeamBBanHeroIds.Count);
    }

    [Fact]
    public void MatchGame_UpdateWalkover_ShouldResetDurationKillsStatsAndSetIsWalkover()
    {
        var seriesId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var playerStats = new List<PlayerGameStats>
        {
            new PlayerGameStats(Guid.NewGuid(), teamA, 5, 8, 1, 10, 15000, 500, 600, null)
        };

        var game = MatchGame.CreateReplay(seriesId, 1, teamA, 1800, 20, 10, 123456, playerStats);
        Assert.False(game.IsWalkover);
        Assert.Equal(ResultSource.ReplayParser, game.ResultSource);
        Assert.NotEmpty(game.PlayerStats);

        game.UpdateWalkover(teamB);

        Assert.True(game.IsWalkover);
        Assert.Equal(ResultSource.Walkover, game.ResultSource);
        Assert.Equal(teamB, game.WinnerTeamId);
        Assert.Equal(0, game.DurationSeconds);
        Assert.Equal(0, game.TeamAKills);
        Assert.Equal(0, game.TeamBKills);
        Assert.Null(game.DotaMatchId);
        Assert.Null(game.FirstPickTeamId);
        Assert.Empty(game.PlayerStats);
        Assert.Empty(game.TeamABanHeroIds);
        Assert.Empty(game.TeamBBanHeroIds);
    }

    [Fact]
    public void MatchSeries_UpdateGameFromReplay_RecalculatesScoresAndStatus_Bo3()
    {
        var series = new MatchSeries(Guid.NewGuid(), "U1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team Alpha", teamB, "Team Beta");

        // Team A wins Game 1 and Game 2 -> 2-0, Completed
        series.RecordGameReplay(teamA, 1800, 20, 10);
        series.RecordGameReplay(teamA, 1900, 22, 12);

        Assert.Equal(2, series.ScoreTeamA);
        Assert.Equal(0, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(teamA, series.WinnerTeamId);

        // Edit Game 1 via Replay: Team B wins Game 1!
        series.UpdateGameFromReplay(1, teamB, 2100, 10, 30, 999111, null);

        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(1, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.InProgress, series.Status);
        Assert.Null(series.WinnerTeamId);
        Assert.Null(series.LoserTeamId);

        // Edit Game 2 via Replay: Team B wins Game 2!
        series.UpdateGameFromReplay(2, teamB, 2200, 8, 32, 999222, null);

        Assert.Equal(0, series.ScoreTeamA);
        Assert.Equal(2, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(teamB, series.WinnerTeamId);
        Assert.Equal(teamA, series.LoserTeamId);
    }

    [Fact]
    public void MatchSeries_UpdateGameWalkover_RecalculatesScoresAndStatus_Bo3()
    {
        var series = new MatchSeries(Guid.NewGuid(), "U1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team Alpha", teamB, "Team Beta");

        // Team A wins Game 1 and Game 2 -> 2-0, Completed
        series.RecordGameReplay(teamA, 1800, 20, 10);
        series.RecordGameReplay(teamA, 1900, 22, 12);

        Assert.Equal(MatchSeriesStatus.Completed, series.Status);

        // Update Game 1 to Walkover for Team B
        series.UpdateGameWalkover(1, teamB);

        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(1, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.InProgress, series.Status);
        Assert.Null(series.WinnerTeamId);

        // Update Game 2 to Walkover for Team B
        series.UpdateGameWalkover(2, teamB);

        Assert.Equal(0, series.ScoreTeamA);
        Assert.Equal(2, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(teamB, series.WinnerTeamId);
        Assert.Equal(teamA, series.LoserTeamId);
    }

    [Fact]
    public void MatchSeries_UpdateGameWalkover_WhenNextGameNumber_ShouldAddWalkoverGame()
    {
        var series = new MatchSeries(Guid.NewGuid(), "U1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team Alpha", teamB, "Team Beta");

        series.RecordGameReplay(teamA, 1800, 20, 10);
        Assert.Single(series.Games);

        // Game 2 not yet recorded. Walkover for Game 2
        series.UpdateGameWalkover(2, teamA);

        Assert.Equal(2, series.Games.Count);
        Assert.Equal(2, series.ScoreTeamA);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(teamA, series.WinnerTeamId);
    }

    [Fact]
    public void MatchSeries_RecalculatesScoresAndStatus_Bo2_Draw()
    {
        var series = new MatchSeries(Guid.NewGuid(), "G1", SeriesFormat.Bo2);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Team Alpha", teamB, "Team Beta");

        series.RecordGameReplay(teamA, 1800, 20, 10);
        series.RecordGameReplay(teamA, 1900, 22, 12);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(teamA, series.WinnerTeamId);

        // Game 2 edited so Team B wins -> 1-1 Draw in Bo2
        series.UpdateGameWalkover(2, teamB);

        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(1, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Null(series.WinnerTeamId);
        Assert.Null(series.LoserTeamId);
    }

    [Fact]
    public async Task UpdateGameWalkoverHandler_WhenSeriesCompleted_ShouldUndoProgressionAndProgressAgain()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);

        var tournament = new Tournament("Walkover Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo1).Value!);
        await tournamentRepo.AddAsync(tournament);

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var matchSF1 = tournament.BracketSlots.First(s => s.Code == "SF1");
        var seriesSF1 = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo1);
        seriesSF1.AssignTeams(teamA, "Alpha", teamB, "Beta");
        seriesSF1.RecordGameReplay(teamA, 1800, 20, 10); // Alpha won, status = Completed
        await matchRepo.AddAsync(seriesSF1);

        // Alpha advanced to GF
        await progressionService.ProgressBracketAsync(seriesSF1);

        var seriesGF = new MatchSeries(tournament.Id, "GF", SeriesFormat.Bo1);
        seriesGF.AssignTeamA(teamA, "Alpha");
        await matchRepo.AddAsync(seriesGF);

        // Now admin updates Game 1 walkover to Team B (Beta)
        var handler = new UpdateGameWalkoverHandler(matchRepo, progressionService);
        await handler.Handle(new UpdateGameWalkoverCommand(seriesSF1.Id, 1, teamB), CancellationToken.None);

        var updatedSF1 = await matchRepo.GetByIdAsync(seriesSF1.Id);
        Assert.NotNull(updatedSF1);
        Assert.Equal(0, updatedSF1.ScoreTeamA);
        Assert.Equal(1, updatedSF1.ScoreTeamB);
        Assert.Equal(teamB, updatedSF1.WinnerTeamId);
        Assert.Equal(MatchSeriesStatus.Completed, updatedSF1.Status);

        // Downstream GF slot should now have Team B (Beta) assigned!
        var updatedGF = await matchRepo.GetByIdAsync(seriesGF.Id);
        Assert.NotNull(updatedGF);
        Assert.Equal(teamB, updatedGF.TeamAId);
        Assert.Equal("Beta", updatedGF.TeamAName);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenSeriesCompleted_WithGameNumber_ShouldSucceedAndReplaceGame()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Replay Edit Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo3).Value!);
        await tournamentRepo.AddAsync(tournament);

        var draft = new DraftSession(tournament.Id, 4);
        var teamA = draft.Teams.First();
        var teamB = draft.Teams.Skip(1).First();

        for (int i = 1; i <= 5; i++)
        {
            var pIdA = Guid.NewGuid();
            var steam64A = (76561198000000000L + i).ToString();
            draft.AddPlayerToPool(pIdA, $"PlayerA_{i}", steam64A);
            teamA.AddStarter(new DraftPlayer(pIdA, $"PlayerA_{i}", steam64A));

            var pIdB = Guid.NewGuid();
            var steam64B = (76561198000000010L + i).ToString();
            draft.AddPlayerToPool(pIdB, $"PlayerB_{i}", steam64B);
            teamB.AddStarter(new DraftPlayer(pIdB, $"PlayerB_{i}", steam64B));
        }
        await draftRepo.AddAsync(draft);

        // Bo3 series completed 2-0 for Team A
        var series = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo3);
        series.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        series.RecordGameReplay(teamA.Id, 1800, 20, 10);
        series.RecordGameReplay(teamA.Id, 1900, 25, 15);
        await matchRepo.AddAsync(series);

        Assert.Equal(MatchSeriesStatus.Completed, series.Status);

        // Mock Parser output where Team B wins
        var mockParser = new TestDotaReplayParserService();
        var radiantPlayers = new List<ParseGoDotaPlayerDto>();
        for (int i = 1; i <= 5; i++)
        {
            radiantPlayers.Add(new ParseGoDotaPlayerDto
            {
                SteamId = 76561198000000000L + i,
                Nome = $"PlayerA_{i}",
                HeroId = i,
                Kills = 2,
                Deaths = 10,
                Assists = 3,
                NetWorth = 10000,
                Gpm = 300,
                Xpm = 400
            });
        }
        var direPlayers = new List<ParseGoDotaPlayerDto>();
        for (int i = 1; i <= 5; i++)
        {
            direPlayers.Add(new ParseGoDotaPlayerDto
            {
                SteamId = 76561198000000010L + i,
                Nome = $"PlayerB_{i}",
                HeroId = 10 + i,
                Kills = 10,
                Deaths = 2,
                Assists = 8,
                NetWorth = 25000,
                Gpm = 700,
                Xpm = 800
            });
        }

        mockParser.ResponseToReturn = new ParseGoDotaOutputDto
        {
            Sumario = new ParseGoDotaSummaryDto
            {
                MatchId = 777888999,
                DuracaoSegundos = 2200,
                Vencedor = "Dire",
                PlacarRadiant = 10,
                PlacarDire = 50,
                DataHoraInicio = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")
            },
            JogadoresEEventos = new ParseGoDotaPlayersAndEventsDto
            {
                Radiant = radiantPlayers,
                Dire = direPlayers
            }
        };

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, mockParser, sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        // Upload replay specifying gameNumber = 1 to replace Game 1
        var result = await handler.Handle(
            new UploadMatchReplayCommand(series.Id, dummyStream, "replace_game1.dem", null, 1),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(777888999, result.DotaMatchId);
        Assert.Equal(teamB.Id, result.WinnerTeamId);
        Assert.Equal(1, result.GameNumber);
        Assert.Equal(1, result.ScoreTeamA);
        Assert.Equal(1, result.ScoreTeamB);
        Assert.Equal("InProgress", result.Status);

        var updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.NotNull(updated);
        Assert.Equal(MatchSeriesStatus.InProgress, updated.Status);
        Assert.Equal(1, updated.ScoreTeamA);
        Assert.Equal(1, updated.ScoreTeamB);
        var game1 = updated.Games.First(g => g.GameNumber == 1);
        Assert.Equal(teamB.Id, game1.WinnerTeamId);
        Assert.Equal(777888999, game1.DotaMatchId);
        Assert.Equal(10, game1.PlayerStats.Count);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenReplayIsSameMatchIdAsReplacedGame_ShouldNotThrowDuplicate()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Replay Duplicate Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo3).Value!);
        await tournamentRepo.AddAsync(tournament);

        var draft = new DraftSession(tournament.Id, 4);
        var teamA = draft.Teams.First();
        var teamB = draft.Teams.Skip(1).First();

        for (int i = 1; i <= 5; i++)
        {
            var pIdA = Guid.NewGuid();
            var steam64A = (76561198000000000L + i).ToString();
            draft.AddPlayerToPool(pIdA, $"PlayerA_{i}", steam64A);
            teamA.AddStarter(new DraftPlayer(pIdA, $"PlayerA_{i}", steam64A));

            var pIdB = Guid.NewGuid();
            var steam64B = (76561198000000010L + i).ToString();
            draft.AddPlayerToPool(pIdB, $"PlayerB_{i}", steam64B);
            teamB.AddStarter(new DraftPlayer(pIdB, $"PlayerB_{i}", steam64B));
        }
        await draftRepo.AddAsync(draft);

        var series = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo3);
        series.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        // Game 1 has DotaMatchId 555000
        series.RecordGameReplay(teamA.Id, 1800, 20, 10, 555000, null);
        await matchRepo.AddAsync(series);

        var mockParser = new TestDotaReplayParserService();
        var radiantPlayers = new List<ParseGoDotaPlayerDto>();
        var direPlayers = new List<ParseGoDotaPlayerDto>();
        for (int i = 1; i <= 5; i++)
        {
            radiantPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 76561198000000000L + i, HeroId = i });
            direPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 76561198000000010L + i, HeroId = 10 + i });
        }

        mockParser.ResponseToReturn = new ParseGoDotaOutputDto
        {
            Sumario = new ParseGoDotaSummaryDto
            {
                MatchId = 555000, // Same Match ID!
                DuracaoSegundos = 2000,
                Vencedor = "Radiant",
                PlacarRadiant = 25,
                PlacarDire = 15,
                DataHoraInicio = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")
            },
            JogadoresEEventos = new ParseGoDotaPlayersAndEventsDto
            {
                Radiant = radiantPlayers,
                Dire = direPlayers
            }
        };

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, mockParser, sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        // Should NOT throw duplicate exception because it is replacing game 1 itself
        var result = await handler.Handle(
            new UploadMatchReplayCommand(series.Id, dummyStream, "game1_reparsed.dem", null, 1),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(555000, result.DotaMatchId);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenReplayIsDuplicateFromDifferentGame_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Replay Duplicate Cup 2",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo3).Value!);
        await tournamentRepo.AddAsync(tournament);

        var draft = new DraftSession(tournament.Id, 4);
        var teamA = draft.Teams.First();
        var teamB = draft.Teams.Skip(1).First();

        for (int i = 1; i <= 5; i++)
        {
            var pIdA = Guid.NewGuid();
            var steam64A = (76561198000000000L + i).ToString();
            draft.AddPlayerToPool(pIdA, $"PlayerA_{i}", steam64A);
            teamA.AddStarter(new DraftPlayer(pIdA, $"PlayerA_{i}", steam64A));

            var pIdB = Guid.NewGuid();
            var steam64B = (76561198000000010L + i).ToString();
            draft.AddPlayerToPool(pIdB, $"PlayerB_{i}", steam64B);
            teamB.AddStarter(new DraftPlayer(pIdB, $"PlayerB_{i}", steam64B));
        }
        await draftRepo.AddAsync(draft);

        var series = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo3);
        series.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        // Game 1 has DotaMatchId 555000
        series.RecordGameReplay(teamA.Id, 1800, 20, 10, 555000, null);
        // Game 2 has DotaMatchId 555001
        series.RecordGameReplay(teamA.Id, 1900, 22, 12, 555001, null);
        await matchRepo.AddAsync(series);

        var mockParser = new TestDotaReplayParserService();
        var radiantPlayers = new List<ParseGoDotaPlayerDto>();
        var direPlayers = new List<ParseGoDotaPlayerDto>();
        for (int i = 1; i <= 5; i++)
        {
            radiantPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 76561198000000000L + i, HeroId = i });
            direPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 76561198000000010L + i, HeroId = 10 + i });
        }

        mockParser.ResponseToReturn = new ParseGoDotaOutputDto
        {
            Sumario = new ParseGoDotaSummaryDto
            {
                MatchId = 555000, // Duplicate of Game 1!
                DuracaoSegundos = 2000,
                Vencedor = "Radiant",
                PlacarRadiant = 25,
                PlacarDire = 15,
                DataHoraInicio = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")
            },
            JogadoresEEventos = new ParseGoDotaPlayersAndEventsDto
            {
                Radiant = radiantPlayers,
                Dire = direPlayers
            }
        };

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, mockParser, sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        // Attempting to replace Game 2 with Game 1's MatchId MUST throw!
        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new UploadMatchReplayCommand(series.Id, dummyStream, "duplicate_game2.dem", null, 2),
            CancellationToken.None));

        Assert.Contains("já foi utilizada em outro confronto", ex.Message);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenSeriesCompleted_WithoutGameNumber_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Replay Completed Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo1, SeriesFormat.Bo1).Value!);
        await tournamentRepo.AddAsync(tournament);

        var series = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo1);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Alpha", teamB, "Beta");
        series.RecordGameReplay(teamA, 1800, 20, 10);
        await matchRepo.AddAsync(series);

        Assert.Equal(MatchSeriesStatus.Completed, series.Status);

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, new TestDotaReplayParserService(), sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new UploadMatchReplayCommand(series.Id, dummyStream, "new.dem", null, null),
            CancellationToken.None));

        Assert.Contains("já foi finalizada", ex.Message);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenGameNumberNotFound_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Replay NotFound Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo3).Value!);
        await tournamentRepo.AddAsync(tournament);

        var series = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Alpha", teamB, "Beta");
        await matchRepo.AddAsync(series);

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, new TestDotaReplayParserService(), sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new UploadMatchReplayCommand(series.Id, dummyStream, "replace.dem", null, 99),
            CancellationToken.None));

        Assert.Contains("Não é possível registrar o Jogo #99 antes de registrar o Jogo #98", ex.Message);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenNextGameNumber_ShouldRecordGame2Successfully()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Replay NextGame Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo3).Value!);
        await tournamentRepo.AddAsync(tournament);

        var draft = new DraftSession(tournament.Id, 4);
        var teamA = draft.Teams.First();
        var teamB = draft.Teams.Skip(1).First();

        for (int i = 1; i <= 5; i++)
        {
            var pIdA = Guid.NewGuid();
            var steam64A = (76561198000000000L + i).ToString();
            draft.AddPlayerToPool(pIdA, $"PlayerA_{i}", steam64A);
            teamA.AddStarter(new DraftPlayer(pIdA, $"PlayerA_{i}", steam64A));

            var pIdB = Guid.NewGuid();
            var steam64B = (76561198000000010L + i).ToString();
            draft.AddPlayerToPool(pIdB, $"PlayerB_{i}", steam64B);
            teamB.AddStarter(new DraftPlayer(pIdB, $"PlayerB_{i}", steam64B));
        }
        await draftRepo.AddAsync(draft);

        var series = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo3);
        series.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        series.RecordGameReplay(teamA.Id, 1800, 20, 10); // Game 1 recorded, status InProgress
        await matchRepo.AddAsync(series);

        Assert.Equal(MatchSeriesStatus.InProgress, series.Status);

        var mockParser = new TestDotaReplayParserService();
        var radiantPlayers = new List<ParseGoDotaPlayerDto>();
        var direPlayers = new List<ParseGoDotaPlayerDto>();
        for (int i = 1; i <= 5; i++)
        {
            radiantPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 76561198000000000L + i, HeroId = i, Kills = 5 });
            direPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 76561198000000010L + i, HeroId = 10 + i, Kills = 1 });
        }

        mockParser.ResponseToReturn = new ParseGoDotaOutputDto
        {
            Sumario = new ParseGoDotaSummaryDto
            {
                MatchId = 888777111,
                DuracaoSegundos = 2100,
                Vencedor = "Radiant",
                PlacarRadiant = 25,
                PlacarDire = 5,
                DataHoraInicio = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")
            },
            JogadoresEEventos = new ParseGoDotaPlayersAndEventsDto
            {
                Radiant = radiantPlayers,
                Dire = direPlayers
            }
        };

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, mockParser, sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        // Upload replay explicitly targeting next gameNumber = 2
        var result = await handler.Handle(
            new UploadMatchReplayCommand(series.Id, dummyStream, "game2.dem", null, 2),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.GameNumber);
        Assert.Equal(teamA.Id, result.WinnerTeamId);
        Assert.Equal(2, result.ScoreTeamA);
        Assert.Equal(0, result.ScoreTeamB);
        Assert.Equal("Completed", result.Status);

        var updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Games.Count);
        Assert.Equal(MatchSeriesStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenNextGameNumberOnCompletedSeries_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Replay NextOnCompleted Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo3, SeriesFormat.Bo3, SeriesFormat.Bo3).Value!);
        await tournamentRepo.AddAsync(tournament);

        var series = new MatchSeries(tournament.Id, "SF1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Alpha", teamB, "Beta");
        series.RecordGameReplay(teamA, 1800, 20, 10);
        series.RecordGameReplay(teamA, 1900, 25, 15); // Bo3 completed 2-0
        await matchRepo.AddAsync(series);

        Assert.Equal(MatchSeriesStatus.Completed, series.Status);

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, new TestDotaReplayParserService(), sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new UploadMatchReplayCommand(series.Id, dummyStream, "game3.dem", null, 3),
            CancellationToken.None));

        Assert.Contains("já foi finalizada", ex.Message);
    }

    [Fact]
    public void MatchSeries_SwitchingModesAcrossRevisions_Bo5()
    {
        var series = new MatchSeries(Guid.NewGuid(), "GF", SeriesFormat.Bo5);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "Alpha", teamB, "Beta");

        // Game 1: Replay (Team A wins)
        series.RecordGameReplay(teamA, 2000, 30, 20, 111001, null);
        // Game 2: Walkover (Team A wins)
        series.UpdateGameWalkover(2, teamA);
        // Game 3: Manual (Team A wins) -> 3-0, Completed!
        series.RecordGameReplay(teamA, 1800, 25, 10);

        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(3, series.ScoreTeamA);
        Assert.Equal(0, series.ScoreTeamB);
        Assert.Equal(teamA, series.WinnerTeamId);

        // Edit Game 3: change Manual to Walkover for Team B -> 2-1, InProgress
        series.UpdateGameWalkover(3, teamB);
        Assert.Equal(MatchSeriesStatus.InProgress, series.Status);
        Assert.Equal(2, series.ScoreTeamA);
        Assert.Equal(1, series.ScoreTeamB);
        Assert.Null(series.WinnerTeamId);

        // Edit Game 2: change Walkover to Replay for Team B -> 1-2, InProgress
        series.UpdateGameFromReplay(2, teamB, 2200, 15, 35, 111002, null);
        Assert.Equal(MatchSeriesStatus.InProgress, series.Status);
        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(2, series.ScoreTeamB);

        // Game 4: Walkover for Team B -> 1-3, Completed! Team B wins Bo5!
        series.UpdateGameWalkover(4, teamB);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(3, series.ScoreTeamB);
        Assert.Equal(teamB, series.WinnerTeamId);
        Assert.Equal(teamA, series.LoserTeamId);
    }
}
