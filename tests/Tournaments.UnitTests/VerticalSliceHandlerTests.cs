using BuildingBlocks.Domain;
using Tournaments.Application.Common.Services;
using Tournaments.Application.Features.Draft.AddPlayerToPool;
using Tournaments.Application.Features.Draft.RemoveCaptain;
using Tournaments.Application.Features.Draft.RenameTeam;
using Tournaments.Application.Features.Draft.SelectCaptain;
using Tournaments.Application.Features.Matches.GetTournamentMatches;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Application.Features.Matches.RecordReplayResult;
using Tournaments.Application.Features.Matches.RecordWalkover;
using Tournaments.Application.Features.Matches.RemoveLastGame;
using Tournaments.Application.Features.Matches.RollbackMatchSeries;
using Tournaments.Application.Features.Matches.UploadMatchReplay;
using Tournaments.Application.Features.Tournaments.AddTeamToTournament;
using Tournaments.Application.Features.Tournaments.AdvanceToPlayoffs;
using Tournaments.Application.Features.Tournaments.CreateTournament;
using Tournaments.Application.Features.Tournaments.GetTournamentById;
using Tournaments.Application.Features.Tournaments.GetTournamentFormatOptions;
using Tournaments.Application.Features.Tournaments.GetTournamentStandings;
using Tournaments.Application.Features.Tournaments.RevertToGroupStage;
using Tournaments.Application.Features.Tournaments.StartTournament;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.ValueObjects;
using Tournaments.Infrastructure.Repositories;
using Xunit;

namespace Tournaments.UnitTests;

public class VerticalSliceHandlerTests
{
    [Fact]
    public async Task CreateTournamentHandler_ShouldCreateTournamentAndDraftSession_WhenDraftModeEnabled()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var handler = new CreateTournamentHandler(tournamentRepo, draftRepo);

        var cmd = new CreateTournamentCommand(
            Name: "The International 2026",
            Type: TournamentType.GroupStageAndPlayoffs,
            PlayoffFormat: PlayoffFormat.DoubleElimination,
            TeamCount: 8,
            GroupCount: 2,
            GroupStageFormat: SeriesFormat.Bo2,
            PlayoffSeriesFormat: SeriesFormat.Bo3,
            GrandFinalSeriesFormat: SeriesFormat.Bo5,
            EnableDraftMode: true
        );

        var response = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("The International 2026", response.Name);
        Assert.NotNull(response.DraftSessionId);

        var createdTournament = await tournamentRepo.GetByIdAsync(response.Id);
        Assert.NotNull(createdTournament);

        var createdDraft = await draftRepo.GetByIdAsync(response.DraftSessionId!.Value);
        Assert.NotNull(createdDraft);
        Assert.Equal(8, createdDraft.TargetTeamCount);
    }

    [Fact]
    public async Task CreateTournamentHandler_ShouldNormalizePlayoffFormat_BasedOnTournamentType()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var handler = new CreateTournamentHandler(tournamentRepo, draftRepo);

        // Mesmo se o Front enviar SingleElimination ou 2 grupos para 6 times em GroupStageAndPlayoffs,
        // o Backend normaliza para DoubleElimination (Upper & Lower) e 1 grupo único.
        var groupTournament = await handler.Handle(new CreateTournamentCommand(
            Name: "Group Stage Cup",
            Type: TournamentType.GroupStageAndPlayoffs,
            PlayoffFormat: PlayoffFormat.SingleElimination,
            TeamCount: 6,
            GroupCount: 2,
            GroupStageFormat: SeriesFormat.Bo1,
            PlayoffSeriesFormat: SeriesFormat.Bo3,
            GrandFinalSeriesFormat: SeriesFormat.Bo5,
            EnableDraftMode: false
        ), CancellationToken.None);

        Assert.Equal("DoubleElimination", groupTournament.PlayoffFormat);
        Assert.Equal(1, groupTournament.GroupCount);
        Assert.Contains(groupTournament.BracketSlots, s => s.Code == "L1");

        // E se enviar DoubleElimination em DirectPlayoffs, normaliza para SingleElimination.
        var directTournament = await handler.Handle(new CreateTournamentCommand(
            Name: "Direct Cup",
            Type: TournamentType.DirectPlayoffs,
            PlayoffFormat: PlayoffFormat.DoubleElimination,
            TeamCount: 4,
            GroupCount: 2,
            GroupStageFormat: SeriesFormat.Bo2,
            PlayoffSeriesFormat: SeriesFormat.Bo3,
            GrandFinalSeriesFormat: SeriesFormat.Bo5,
            EnableDraftMode: false
        ), CancellationToken.None);

        Assert.Equal("SingleElimination", directTournament.PlayoffFormat);
        Assert.Equal(1, directTournament.GroupCount);
        Assert.DoesNotContain(directTournament.BracketSlots, s => s.Code == "L1");
    }

    [Fact]
    public async Task AddTeamToTournamentHandler_ShouldAddTeamToTournament()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var createHandler = new CreateTournamentHandler(tournamentRepo, draftRepo);

        var t = await createHandler.Handle(new CreateTournamentCommand(
            "Test Cup",
            TournamentType.DirectPlayoffs,
            PlayoffFormat.SingleElimination,
            4,
            1,
            null,
            SeriesFormat.Bo3,
            SeriesFormat.Bo3,
            false
        ), CancellationToken.None);

        var addTeamHandler = new AddTeamToTournamentHandler(tournamentRepo);
        var teamId = Guid.NewGuid();
        await addTeamHandler.Handle(new AddTeamToTournamentCommand(t.Id, teamId, "Team Spirit"), CancellationToken.None);

        var getHandler = new GetTournamentByIdHandler(tournamentRepo);
        var updated = await getHandler.Handle(new GetTournamentByIdQuery(t.Id), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Single(updated.Teams);
        Assert.Equal("Team Spirit", updated.Teams[0].Name);
    }

    [Fact]
    public async Task DraftHandlers_ShouldAddPlayerAndSelectCaptain_NotifyingRealtime()
    {
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();
        var tournamentId = Guid.NewGuid();
        var session = new DraftSession(tournamentId, 4);
        await draftRepo.AddAsync(session);

        var addPlayerHandler = new AddPlayerToPoolHandler(draftRepo, notifier);
        var playerId = Guid.NewGuid();
        var snapshotAfterAdd = await addPlayerHandler.Handle(
            new AddPlayerToPoolCommand(session.Id, playerId, "Miracle-", "steam_123"),
            CancellationToken.None
        );

        Assert.Single(snapshotAfterAdd.PlayerPool);
        Assert.Equal("Miracle-", snapshotAfterAdd.PlayerPool[0].Name);
        Assert.Contains(notifier.Notifications, n => n.Contains("Snapshot_v2"));

        var selectCaptainHandler = new SelectCaptainHandler(draftRepo, notifier);
        var snapshotAfterCaptain = await selectCaptainHandler.Handle(
            new SelectCaptainCommand(session.Id, playerId, snapshotAfterAdd.DraftVersion),
            CancellationToken.None
        );

        Assert.Empty(snapshotAfterCaptain.PlayerPool);
        Assert.Equal(4, snapshotAfterCaptain.Teams.Count); // Cards de equipes pré-criados fixos
        Assert.Equal(playerId, snapshotAfterCaptain.Teams[0].CaptainId);
        Assert.Equal("Time 1", snapshotAfterCaptain.Teams[0].Name);
        Assert.Single(snapshotAfterCaptain.Teams[0].Starters);
        Assert.Equal("Miracle-", snapshotAfterCaptain.Teams[0].Starters[0].Name);
    }

    [Fact]
    public async Task DraftHandlers_ShouldHandleRemoveCaptainAndConcurrencyValidation()
    {
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();
        var tournamentId = Guid.NewGuid();
        var session = new DraftSession(tournamentId, 4);
        await draftRepo.AddAsync(session);

        var addPlayerHandler = new AddPlayerToPoolHandler(draftRepo, notifier);
        var selectCaptainHandler = new SelectCaptainHandler(draftRepo, notifier);
        var removeCaptainHandler = new RemoveCaptainHandler(draftRepo, notifier);

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var p3Id = Guid.NewGuid();
        var p4Id = Guid.NewGuid();
        var p5Id = Guid.NewGuid();

        await addPlayerHandler.Handle(new AddPlayerToPoolCommand(session.Id, p1Id, "Artour", "steam_1"), CancellationToken.None);
        await addPlayerHandler.Handle(new AddPlayerToPoolCommand(session.Id, p2Id, "Puppey", "steam_2"), CancellationToken.None);
        await addPlayerHandler.Handle(new AddPlayerToPoolCommand(session.Id, p3Id, "Topson", "steam_3"), CancellationToken.None);
        await addPlayerHandler.Handle(new AddPlayerToPoolCommand(session.Id, p4Id, "Miracle", "steam_4"), CancellationToken.None);
        var snap = await addPlayerHandler.Handle(new AddPlayerToPoolCommand(session.Id, p5Id, "Ame", "steam_5"), CancellationToken.None);

        Assert.Equal(5, snap.PlayerPool.Count);

        // Seleciona capitão P1
        snap = await selectCaptainHandler.Handle(new SelectCaptainCommand(session.Id, p1Id, snap.DraftVersion), CancellationToken.None);
        Assert.Equal(4, snap.Teams.Count);
        Assert.Equal(p1Id, snap.Teams[0].CaptainId);
        Assert.Equal("Time 1", snap.Teams[0].Name);

        // Remove capitão P1 (Não desfaz o time! Mantém card e libera slot 1)
        var teamId = snap.Teams[0].Id;
        snap = await removeCaptainHandler.Handle(new RemoveCaptainCommand(session.Id, teamId, snap.DraftVersion), CancellationToken.None);
        Assert.Equal(4, snap.Teams.Count); // O time continua na lista
        Assert.Null(snap.Teams[0].CaptainId); // Slot 1 vago
        Assert.Empty(snap.Teams[0].Starters);
        Assert.Contains(snap.PlayerPool, p => p.Id == p1Id); // Capitão voltou para a pool

        // Teste de concorrência otimista (versão desatualizada deve falhar)
        var outdatedVersion = snap.DraftVersion - 1;
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            selectCaptainHandler.Handle(new SelectCaptainCommand(session.Id, p1Id, outdatedVersion), CancellationToken.None)
        );
        Assert.Contains("Conflito de versão no Draft", ex.Message);
    }

    [Fact]
    public async Task MatchHandlers_ShouldRecordReplayResultAndRollback()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var series = new MatchSeries(Guid.NewGuid(), "U1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "OG", teamB, "PSG.LGD");
        await matchRepo.AddAsync(series);

        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        await replayHandler.Handle(
            new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 1, teamA, 2400, 30, 20, 1001, null)),
            CancellationToken.None
        );

        var updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.ScoreTeamA);
        Assert.Equal(0, updated.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.InProgress, updated.Status);

        // Win 2nd game to complete series
        await replayHandler.Handle(
            new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 2, teamA, 1800, 25, 10, 1002, null)),
            CancellationToken.None
        );

        updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.Equal(MatchSeriesStatus.Completed, updated!.Status);
        Assert.Equal(teamA, updated.WinnerTeamId);

        // Test Rollback
        var rollbackHandler = new RollbackMatchSeriesHandler(matchRepo, progressionService);
        await rollbackHandler.Handle(new RollbackMatchSeriesCommand(series.Id), CancellationToken.None);

        var rolledBack = await matchRepo.GetByIdAsync(series.Id);
        Assert.Equal(MatchSeriesStatus.ReadyToPlay, rolledBack!.Status);
        Assert.Equal(0, rolledBack.ScoreTeamA);
        Assert.Null(rolledBack.WinnerTeamId);
    }

    [Fact]
    public async Task MatchHandlers_ShouldRecordReplayResult_AndWalkover()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var tournamentId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();

        var series = new MatchSeries(tournamentId, "U1", SeriesFormat.Bo3);
        series.AssignTeams(teamAId, "OG", teamBId, "PSG.LGD");
        await matchRepo.AddAsync(series);

        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var walkoverHandler = new RecordWalkoverHandler(matchRepo, progressionService);

        // 1. Lança jogo 1 via Replay Ingestion (com dados ricos de jogadores)
        var playerStats = new List<PlayerMatchStatsDto>
        {
            new(Guid.NewGuid(), teamAId, HeroId: 1, Kills: 12, Deaths: 2, Assists: 15, NetWorth: 25000, GoldPerMinute: 720, ExperiencePerMinute: 800, ItemIds: [1, 2, 3])
        };

        var replayDto = new MatchResultIngestionDto(
            MatchSeriesId: series.Id,
            GameNumber: 1,
            WinnerTeamId: teamAId,
            DurationSeconds: 2450,
            TeamAKills: 35,
            TeamBKills: 18,
            DotaMatchId: 7891234567,
            PlayerStats: playerStats
        );

        await replayHandler.Handle(new RecordReplayResultCommand(replayDto), CancellationToken.None);

        var updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.NotNull(updated);
        Assert.Single(updated.Games);
        Assert.Equal(1, updated.ScoreTeamA);
        Assert.Equal(0, updated.ScoreTeamB);
        Assert.Single(updated.Games.First().PlayerStats);

        // 2. Jogo 2: Team A vence por W.O. (fecha a série 2x0)
        await walkoverHandler.Handle(new RecordWalkoverCommand(series.Id, teamAId), CancellationToken.None);

        updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.Equal(2, updated!.ScoreTeamA);
        Assert.Equal(MatchSeriesStatus.Completed, updated.Status);
        Assert.Equal(teamAId, updated.WinnerTeamId);
    }

    [Fact]
    public async Task GetTournamentFormatOptions_When5Teams_ShouldOnlyReturnGroupStage()
    {
        var handler = new GetTournamentFormatOptionsHandler();
        var result = await handler.Handle(new GetTournamentFormatOptionsQuery(5), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(5, result.SelectedTeamCount);
        Assert.Single(result.AvailableFormats);
        Assert.Equal(TournamentType.GroupStageAndPlayoffs.ToString(), result.AvailableFormats[0].Type);
        Assert.Equal([1], result.AvailableFormats[0].AllowedGroupCounts);
    }

    [Fact]
    public async Task GetTournamentFormatOptions_When8Teams_ShouldReturnBothFormatsAndAllow2Groups()
    {
        var handler = new GetTournamentFormatOptionsHandler();
        var result = await handler.Handle(new GetTournamentFormatOptionsQuery(8), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(8, result.SelectedTeamCount);
        Assert.Equal(2, result.AvailableFormats.Count);

        var direct = result.AvailableFormats.First(f => f.Type == TournamentType.DirectPlayoffs.ToString());
        Assert.Equal([1], direct.AllowedGroupCounts);

        var groupStage = result.AvailableFormats.First(f => f.Type == TournamentType.GroupStageAndPlayoffs.ToString());
        Assert.Equal([1, 2], groupStage.AllowedGroupCounts);
    }

    [Fact]
    public async Task GetTournamentFormatOptions_WhenNoTeamCount_ShouldReturnAllSupportedCounts()
    {
        var handler = new GetTournamentFormatOptionsHandler();
        var result = await handler.Handle(new GetTournamentFormatOptionsQuery(null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.SelectedTeamCount);
        Assert.Equal([4, 5, 6, 7, 8], result.SupportedTeamCounts);
        Assert.Equal(2, result.AvailableFormats.Count);
    }

    [Fact]
    public async Task GetTournamentMatches_ShouldReturnAllMatchesForTournament()
    {
        var matchRepo = new InMemoryMatchSeriesRepository();
        var tournamentId = Guid.NewGuid();

        var match1 = new MatchSeries(tournamentId, "U1", SeriesFormat.Bo3);
        var match2 = new MatchSeries(tournamentId, "L1", SeriesFormat.Bo3);
        var otherTournamentMatch = new MatchSeries(Guid.NewGuid(), "U1", SeriesFormat.Bo3);

        await matchRepo.AddAsync(match1);
        await matchRepo.AddAsync(match2);
        await matchRepo.AddAsync(otherTournamentMatch);

        var handler = new GetTournamentMatchesHandler(matchRepo);
        var matches = await handler.Handle(new GetTournamentMatchesQuery(tournamentId), CancellationToken.None);

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.SlotCode == "U1");
        Assert.Contains(matches, m => m.SlotCode == "L1");
    }

    [Fact]
    public async Task RenameTeamHandler_ShouldEnforceRulesAndRenameSuccessfully()
    {
        var draftRepo = new InMemoryDraftSessionRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var notifier = new TestRealtimeNotifier();

        var tournament = new Tournament("Major",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);
        await tournamentRepo.AddAsync(tournament);

        var session = new DraftSession(tournament.Id, 4);
        await draftRepo.AddAsync(session);

        var renameHandler = new RenameTeamHandler(draftRepo, tournamentRepo, notifier);
        var targetTeam = session.Teams.First();
        tournament.AddTeam(targetTeam.Id, targetTeam.Name);
        await tournamentRepo.UpdateAsync(tournament);

        // 1. Tentar renomear como capitão (não-admin) antes de completar o draft deve falhar
        var exBeforeComplete = await Assert.ThrowsAsync<DomainException>(() =>
            renameHandler.Handle(new RenameTeamCommand(session.Id, targetTeam.Id, "Team Spirit", session.DraftVersion, IsAdmin: false), CancellationToken.None)
        );
        Assert.Contains("Apenas administradores do torneio têm permissão", exBeforeComplete.Message);

        // 1.1 Administrador pode renomear a qualquer momento (mesmo antes da conclusão do draft)
        var earlyAdminSnapshot = await renameHandler.Handle(
            new RenameTeamCommand(session.Id, targetTeam.Id, "Early Admin Name", session.DraftVersion, IsAdmin: true),
            CancellationToken.None
        );
        Assert.Equal("Early Admin Name", earlyAdminSnapshot.Teams.First(t => t.Id == targetTeam.Id).Name);

        // Preenche jogadores e avança o draft
        var players = Enumerable.Range(1, 20)
            .Select(i => new DraftPlayer(Guid.NewGuid(), $"Player_{i}"))
            .ToList();

        // Adiciona todos os 20 jogadores na pool durante SelectingCaptains
        foreach (var p in players)
        {
            session.AddPlayerToPool(p.Id, p.Name);
        }

        // Seleciona capitães
        for (int i = 0; i < 4; i++)
        {
            session.SelectCaptain(players[i].Id, session.DraftVersion);
        }

        session.FinalizeCaptainOrder(PickOrderType.Manual, session.Teams.Select(t => t.Id).ToList(), session.DraftVersion);

        // 16 picks de titulares
        for (int i = 4; i < 20; i++)
        {
            session.PickStarterPlayer(players[i].Id, session.DraftVersion);
        }

        // Conclui draft
        session.CompleteDraft(session.DraftVersion);
        await draftRepo.UpdateAsync(session);

        // 2. Renomeia com sucesso após draft completo e 5 titulares
        var snapshot = await renameHandler.Handle(
            new RenameTeamCommand(session.Id, targetTeam.Id, "Team Spirit", session.DraftVersion),
            CancellationToken.None
        );

        Assert.Equal("Team Spirit", snapshot.Teams.First(t => t.Id == targetTeam.Id).Name);

        // Verifica sincronização com o Torneio
        var updatedTournament = await tournamentRepo.GetByIdAsync(tournament.Id);
        Assert.NotNull(updatedTournament);
        Assert.Equal("Team Spirit", updatedTournament.Teams.First(t => t.Id == targetTeam.Id).Name);

        // 3. Se o torneio avançar para os Playoffs, deve bloquear a renomeação de qualquer equipe
        foreach (var t in session.Teams.Skip(1)) { tournament.AddTeam(t.Id, t.Name); }
tournament.Start();
        tournament.GeneratePlayoffs();
        await tournamentRepo.UpdateAsync(tournament);

        var exPlayoffs = await Assert.ThrowsAsync<DomainException>(() =>
            renameHandler.Handle(
                new RenameTeamCommand(session.Id, targetTeam.Id, "Team Spirit Late", session.DraftVersion, IsAdmin: true),
                CancellationToken.None
            )
        );
        Assert.Contains("Playoffs", exPlayoffs.Message);
    }

    [Fact]
    public async Task RenameTeam_Captain_ShouldOnlyRenameOwnTeam_And_FailOnOtherTeams_OrOtherTournaments()
    {
        var draftRepo = new InMemoryDraftSessionRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var notifier = new TestRealtimeNotifier();
        var handler = new RenameTeamHandler(draftRepo, tournamentRepo, notifier);

        // 1. Torneio A
        var tournamentA = new Tournament("Torneio A",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);
        await tournamentRepo.AddAsync(tournamentA);

        var sessionA = new DraftSession(tournamentA.Id, 4);
        var p0 = new DraftPlayer(Guid.NewGuid(), "Capitão_Alpha", "steam-user-alpha");
        var p1 = new DraftPlayer(Guid.NewGuid(), "Capitão_Beta", "steam-user-beta");
        var poolA = new List<DraftPlayer> { p0, p1 };
        for (int i = 2; i < 20; i++)
        {
            poolA.Add(new DraftPlayer(Guid.NewGuid(), $"Player_{i}"));
        }

        foreach (var p in poolA) sessionA.AddPlayerToPool(p.Id, p.Name, p.ExternalUserId);
        sessionA.SelectCaptain(p0.Id, sessionA.DraftVersion);
        sessionA.SelectCaptain(p1.Id, sessionA.DraftVersion);
        sessionA.SelectCaptain(poolA[2].Id, sessionA.DraftVersion);
        sessionA.SelectCaptain(poolA[3].Id, sessionA.DraftVersion);

        sessionA.FinalizeCaptainOrder(PickOrderType.Manual, sessionA.Teams.Select(t => t.Id).ToList(), sessionA.DraftVersion);

        for (int i = 4; i < 20; i++)
        {
            sessionA.PickStarterPlayer(poolA[i].Id, sessionA.DraftVersion);
        }
        sessionA.CompleteDraft(sessionA.DraftVersion);
        await draftRepo.AddAsync(sessionA);

        var teamAlpha = sessionA.Teams.First(t => t.CaptainId == p0.Id);
        var teamBeta = sessionA.Teams.First(t => t.CaptainId == p1.Id);

        // 2. Não-administrador tenta alterar qualquer time -> DEVE FALHAR (apenas Admin pode renomear)
        var exNonAdmin = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new RenameTeamCommand(
                sessionA.Id,
                teamBeta.Id,
                "Hacked by Alpha",
                sessionA.DraftVersion,
                ActingUserId: "steam-user-alpha",
                IsAdmin: false
            ), CancellationToken.None)
        );
        Assert.Contains("Apenas administradores do torneio têm permissão", exNonAdmin.Message);

        // 3. Administrador altera a equipe -> SUCESSO
        var snapshotSuccess = await handler.Handle(new RenameTeamCommand(
            sessionA.Id,
            teamAlpha.Id,
            "Alpha Wolves",
            sessionA.DraftVersion,
            ActingUserId: null,
            IsAdmin: true
        ), CancellationToken.None);

        Assert.Equal("Alpha Wolves", snapshotSuccess.Teams.First(t => t.Id == teamAlpha.Id).Name);

        // 4. Torneio B completamente separado: Capitão Alpha tenta mexer em uma equipe do Torneio B onde ele NÃO é capitão
        var tournamentB = new Tournament("Torneio B",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);
        await tournamentRepo.AddAsync(tournamentB);

        var sessionB = new DraftSession(tournamentB.Id, 4);
        var poolB = Enumerable.Range(1, 20).Select(i => new DraftPlayer(Guid.NewGuid(), $"B_Player_{i}", $"steam-b-{i}")).ToList();
        foreach (var p in poolB) sessionB.AddPlayerToPool(p.Id, p.Name, p.ExternalUserId);
        for (int i = 0; i < 4; i++) sessionB.SelectCaptain(poolB[i].Id, sessionB.DraftVersion);
        sessionB.FinalizeCaptainOrder(PickOrderType.Manual, sessionB.Teams.Select(t => t.Id).ToList(), sessionB.DraftVersion);
        for (int i = 4; i < 20; i++) sessionB.PickStarterPlayer(poolB[i].Id, sessionB.DraftVersion);
        sessionB.CompleteDraft(sessionB.DraftVersion);
        await draftRepo.AddAsync(sessionB);

        var teamInTournamentB = sessionB.Teams.First();

        // Não-admin tenta alterar equipe no Torneio B -> DEVE FALHAR
        var exOtherTournament = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new RenameTeamCommand(
                sessionB.Id,
                teamInTournamentB.Id,
                "Alpha Invasor",
                sessionB.DraftVersion,
                ActingUserId: "steam-user-alpha",
                IsAdmin: false
            ), CancellationToken.None)
        );
        Assert.Contains("Apenas administradores do torneio têm permissão", exOtherTournament.Message);

        // 5. Admin com IsAdmin = true consegue renomear qualquer equipe
        var snapshotAdmin = await handler.Handle(new RenameTeamCommand(
            sessionB.Id,
            teamInTournamentB.Id,
            "Admin Renamed Team",
            sessionB.DraftVersion,
            ActingUserId: "admin-id",
            IsAdmin: true
        ), CancellationToken.None);

        Assert.Equal("Admin Renamed Team", snapshotAdmin.Teams.First(t => t.Id == teamInTournamentB.Id).Name);
    }

    [Fact]
    public async Task AddLatePlayerHandler_DuringDraft_ShouldSucceed_And_ExposeRolesInSnapshot()
    {
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();
        var handler = new Tournaments.Application.Features.Draft.AddLatePlayerToPool.AddLatePlayerToPoolHandler(draftRepo, notifier);

        var session = new DraftSession(Guid.NewGuid(), 4);
        var players = Enumerable.Range(1, 20).Select(i => new DraftPlayer(Guid.NewGuid(), $"Player_{i}")).ToList();
        foreach (var p in players) session.AddPlayerToPool(p.Id, p.Name);
        for (int i = 0; i < 4; i++) session.SelectCaptain(players[i].Id, session.DraftVersion);
        session.FinalizeCaptainOrder(PickOrderType.Manual, session.Teams.Select(t => t.Id).ToList(), session.DraftVersion);

        // Executa alguns picks (está em DraftingStarters)
        session.PickStarterPlayer(players[4].Id, session.DraftVersion);
        session.PickStarterPlayer(players[5].Id, session.DraftVersion);
        await draftRepo.AddAsync(session);

        // Jogador atrasado entra de última hora com roles
        var latePlayerId = Guid.NewGuid();
        var command = new Tournaments.Application.Features.Draft.AddLatePlayerToPool.AddLatePlayerToPoolCommand(
            session.Id,
            latePlayerId,
            "LateProPlayer",
            "steam-late-999",
            new[] { "Carry", "Mid" },
            "Carry",
            80, // RankTier Immortal
            "https://avatar.url/late.jpg",
            session.DraftVersion
        );

        var snapshot = await handler.Handle(command, CancellationToken.None);

        Assert.Contains(snapshot.PlayerPool, p => p.Id == latePlayerId);
        var addedInSnapshot = snapshot.PlayerPool.First(p => p.Id == latePlayerId);
        Assert.Equal("LateProPlayer", addedInSnapshot.Name);
        Assert.Equal("Carry", addedInSnapshot.PreferredRole);
        Assert.Contains("Mid", addedInSnapshot.Roles!);
        Assert.Equal(80, addedInSnapshot.RankTier);
        Assert.Single(notifier.Notifications); // Notificou SignalR em tempo real
    }

    [Fact]
    public async Task CloseRegistrationsHandler_ShouldTransitionStatusToDrafting()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var handler = new Tournaments.Application.Features.Tournaments.CloseRegistrations.CloseRegistrationsHandler(tournamentRepo);

        var tournament = new Tournament("Copa In-House",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.SingleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        // Abre inscrições
        tournament.OpenRegistrations();
        await tournamentRepo.AddAsync(tournament);

        // Admin encerra inscrições
        var command = new Tournaments.Application.Features.Tournaments.CloseRegistrations.CloseRegistrationsCommand(tournament.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(TournamentStatus.Drafting.ToString(), result.Status);
    }

    [Fact]
    public async Task GetTournamentStandingsHandler_ShouldCalculatePointsMapDiffAndSortCorrectly()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();

        var tournament = new Tournament("Dota 2 Group Stage",
            TournamentFormatConfig.Create(TournamentType.GroupStageAndPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo2, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var teamCId = Guid.NewGuid();
        var teamDId = Guid.NewGuid();

        tournament.AddTeam(teamAId, "Time A");
        tournament.AddTeam(teamBId, "Time B");
        tournament.AddTeam(teamCId, "Time C");
        tournament.AddTeam(teamDId, "Time D");
        await tournamentRepo.AddAsync(tournament);

        // Partida 1: Time A 2x0 Time B (Time A soma 3pts, saldo +2. Time B soma 0pts, saldo -2)
        var s1 = new MatchSeries(tournament.Id, "G1", SeriesFormat.Bo2);
        s1.AssignTeams(teamAId, "Time A", teamBId, "Time B");
        s1.RecordGameReplay(teamAId, 2000, 25, 10);
        s1.RecordGameReplay(teamAId, 2200, 30, 15);
        await matchRepo.AddAsync(s1);

        // Partida 2: Time C 1x1 Time D (Empate: 1pt para cada, saldo 0 para ambos)
        var s2 = new MatchSeries(tournament.Id, "G2", SeriesFormat.Bo2);
        s2.AssignTeams(teamCId, "Time C", teamDId, "Time D");
        s2.RecordGameReplay(teamCId, 2100, 20, 15);
        s2.RecordGameReplay(teamDId, 2300, 22, 18);
        await matchRepo.AddAsync(s2);

        // Partida 3: Time C 2x0 Time A (Time C soma +3pts -> total 4pts, saldo +2)
        // Time A perde 0x2 -> continua com 3pts, saldo 0
        var s3 = new MatchSeries(tournament.Id, "G3", SeriesFormat.Bo2);
        s3.AssignTeams(teamCId, "Time C", teamAId, "Time A");
        s3.RecordGameReplay(teamCId, 1900, 28, 12);
        s3.RecordGameReplay(teamCId, 2000, 31, 14);
        await matchRepo.AddAsync(s3);

        var handler = new GetTournamentStandingsHandler(tournamentRepo, matchRepo);
        var response = await handler.Handle(new GetTournamentStandingsQuery(tournament.Id), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(tournament.Id, response.TournamentId);
        Assert.Equal(4, response.Standings.Count);
        Assert.True(response.Version >= 1);

        // 1º Lugar: Time C (4 pontos, 1V 1E 0D, saldo +2)
        Assert.Equal("Time C", response.Standings[0].TeamName);
        Assert.Equal(4, response.Standings[0].Points);
        Assert.Equal(2, response.Standings[0].PlayedMatches);
        Assert.Equal(1, response.Standings[0].WonMatches);
        Assert.Equal(1, response.Standings[0].DrawnMatches);
        Assert.Equal(0, response.Standings[0].LostMatches);
        Assert.Equal(3, response.Standings[0].MapWins);
        Assert.Equal(1, response.Standings[0].MapLosses);
        Assert.Equal(2, response.Standings[0].MapDifferential);

        // 2º Lugar: Time A (3 pontos, 1V 0E 1D, saldo 0)
        Assert.Equal("Time A", response.Standings[1].TeamName);
        Assert.Equal(3, response.Standings[1].Points);
        Assert.Equal(0, response.Standings[1].MapDifferential);

        // 3º Lugar: Time D (1 ponto, 0V 1E 0D, saldo 0)
        Assert.Equal("Time D", response.Standings[2].TeamName);
        Assert.Equal(1, response.Standings[2].Points);
        Assert.Equal(0, response.Standings[2].MapDifferential);

        // 4º Lugar: Time B (0 pontos, 0V 0E 1D, saldo -2)
        Assert.Equal("Time B", response.Standings[3].TeamName);
        Assert.Equal(0, response.Standings[3].Points);
        Assert.Equal(-2, response.Standings[3].MapDifferential);
    }

    [Fact]
    public async Task StartTournamentHandler_WhenDirectPlayoffs_ShouldGenerateMatchesFromBracketSlots()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();

        var tournament = new Tournament("Direct Playoffs Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var t4 = Guid.NewGuid();

        tournament.AddTeam(t1, "Team 1");
        tournament.AddTeam(t2, "Team 2");
        tournament.AddTeam(t3, "Team 3");
        tournament.AddTeam(t4, "Team 4");
        await tournamentRepo.AddAsync(tournament);

        var startHandler = new StartTournamentHandler(tournamentRepo, matchRepo);
        await startHandler.Handle(new StartTournamentCommand(tournament.Id), CancellationToken.None);

        var updated = await tournamentRepo.GetByIdAsync(tournament.Id);
        Assert.NotNull(updated);
        Assert.Equal(TournamentStatus.Playoffs, updated.Status);

        var matches = await matchRepo.GetByTournamentIdAsync(tournament.Id);
        Assert.Equal(4, matches.Count); // U1, L1, LF, GF

        var u1 = matches.First(m => m.SlotCode == "U1");
        Assert.Equal(t1, u1.TeamAId);
        Assert.Equal(t2, u1.TeamBId);

        var l1 = matches.First(m => m.SlotCode == "L1");
        Assert.Equal(t3, l1.TeamAId);
        Assert.Equal(t4, l1.TeamBId);

        var lf = matches.First(m => m.SlotCode == "LF");
        Assert.Null(lf.TeamAId);
        Assert.Null(lf.TeamBId);
    }

    [Fact]
    public async Task StartTournamentHandler_WhenGroupStage_ShouldGenerateRoundRobinMatches()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();

        var tournament = new Tournament("Group Stage Cup",
            TournamentFormatConfig.Create(TournamentType.GroupStageAndPlayoffs, PlayoffFormat.DoubleElimination, 4, 1).Value!,
            SeriesConfig.Create(SeriesFormat.Bo2, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        tournament.AddTeam(Guid.NewGuid(), "T1");
        tournament.AddTeam(Guid.NewGuid(), "T2");
        tournament.AddTeam(Guid.NewGuid(), "T3");
        tournament.AddTeam(Guid.NewGuid(), "T4");
        await tournamentRepo.AddAsync(tournament);

        var startHandler = new StartTournamentHandler(tournamentRepo, matchRepo);
        await startHandler.Handle(new StartTournamentCommand(tournament.Id), CancellationToken.None);

        var updated = await tournamentRepo.GetByIdAsync(tournament.Id);
        Assert.NotNull(updated);
        Assert.Equal(TournamentStatus.GroupStage, updated.Status);

        var matches = await matchRepo.GetByTournamentIdAsync(tournament.Id);
        // 4 teams, 1 group round robin = 4*3/2 = 6 matches
        Assert.Equal(6, matches.Count);
        Assert.All(matches, m => Assert.StartsWith("G_", m.SlotCode));
        Assert.All(matches, m => Assert.Equal(SeriesFormat.Bo2, m.Format));
    }

    [Fact]
    public async Task AdvanceToPlayoffs_AndRevertToGroupStage_ShouldEnforceSafetyLock()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);

        var tournament = new Tournament("Stage Transition Cup",
            TournamentFormatConfig.Create(TournamentType.GroupStageAndPlayoffs, PlayoffFormat.DoubleElimination, 4, 1).Value!,
            SeriesConfig.Create(SeriesFormat.Bo2, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var t4 = Guid.NewGuid();

        tournament.AddTeam(t1, "Team 1");
        tournament.AddTeam(t2, "Team 2");
        tournament.AddTeam(t3, "Team 3");
        tournament.AddTeam(t4, "Team 4");
        await tournamentRepo.AddAsync(tournament);

        var startHandler = new StartTournamentHandler(tournamentRepo, matchRepo);
        await startHandler.Handle(new StartTournamentCommand(tournament.Id), CancellationToken.None);

        var advanceHandler = new AdvanceToPlayoffsHandler(tournamentRepo, matchRepo);
        var revertHandler = new RevertToGroupStageHandler(tournamentRepo, matchRepo);

        // 1. Tentar avançar antes de todas as partidas estarem concluídas deve falhar
        var exIncomplete = await Assert.ThrowsAsync<DomainException>(() =>
            advanceHandler.Handle(new AdvanceToPlayoffsCommand(tournament.Id), CancellationToken.None));
        Assert.Contains("concluídas", exIncomplete.Message);

        // Conclui todas as partidas dos grupos com vitórias
        var groupMatches = await matchRepo.GetByTournamentIdAsync(tournament.Id);
        foreach (var m in groupMatches)
        {
            m.RecordGameReplay(m.TeamAId!.Value, 1800, 20, 10);
            m.RecordGameReplay(m.TeamAId!.Value, 1900, 22, 12);
            await matchRepo.UpdateAsync(m);
        }

        // 2. Avança para os Playoffs com sucesso
        var advanceResult = await advanceHandler.Handle(new AdvanceToPlayoffsCommand(tournament.Id), CancellationToken.None);
        Assert.Equal(TournamentStatus.Playoffs.ToString(), advanceResult.Status);

        var allMatchesAfterAdvance = await matchRepo.GetByTournamentIdAsync(tournament.Id);
        // 6 group matches + 4 playoff matches = 10 matches
        Assert.Equal(10, allMatchesAfterAdvance.Count);

        // 3. Reverter para a fase de grupos antes de qualquer jogo de playoff deve ter sucesso
        var revertResult = await revertHandler.Handle(new RevertToGroupStageCommand(tournament.Id), CancellationToken.None);
        Assert.Equal(TournamentStatus.GroupStage.ToString(), revertResult.Status);

        var allMatchesAfterRevert = await matchRepo.GetByTournamentIdAsync(tournament.Id);
        Assert.Equal(6, allMatchesAfterRevert.Count); // partidas de playoff foram limpas
        Assert.All(allMatchesAfterRevert, m => Assert.StartsWith("G_", m.SlotCode));

        // 4. Avança novamente
        await advanceHandler.Handle(new AdvanceToPlayoffsCommand(tournament.Id), CancellationToken.None);

        // 5. Registra 1 jogo em uma partida de playoff
        var playoffMatches = (await matchRepo.GetByTournamentIdAsync(tournament.Id))
            .Where(m => !m.SlotCode.StartsWith("G", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var u1 = playoffMatches.First(m => m.SlotCode == "U1");

        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        await replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(u1.Id, 1, u1.TeamAId!.Value, 2000, 25, 10, 1001, null)), CancellationToken.None);

        // 6. Tentar reverter agora deve falhar por segurança (invariante de negócio)
        var exBlocked = await Assert.ThrowsAsync<DomainException>(() =>
            revertHandler.Handle(new RevertToGroupStageCommand(tournament.Id), CancellationToken.None));
        Assert.Contains("já possuem partidas disputadas com mapas jogados", exBlocked.Message);
    }

    [Fact]
    public async Task BracketProgression_ShouldAdvanceWinnersLosersAndCrownChampion()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var walkoverHandler = new RecordWalkoverHandler(matchRepo, progressionService);

        var tournament = new Tournament("Champions League",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var t4 = Guid.NewGuid();

        tournament.AddTeam(t1, "Team 1");
        tournament.AddTeam(t2, "Team 2");
        tournament.AddTeam(t3, "Team 3");
        tournament.AddTeam(t4, "Team 4");
        await tournamentRepo.AddAsync(tournament);

        var startHandler = new StartTournamentHandler(tournamentRepo, matchRepo);
        await startHandler.Handle(new StartTournamentCommand(tournament.Id), CancellationToken.None);

        var matches = (await matchRepo.GetByTournamentIdAsync(tournament.Id)).ToDictionary(m => m.SlotCode);

        // Jogo U1: Team 1 vs Team 2 (Bo3). Team 1 vence.
        // U1 Winner -> GF (Team A), Loser -> LF (Team A)
        var u1 = matches["U1"];
        await walkoverHandler.Handle(new RecordWalkoverCommand(u1.Id, t1), CancellationToken.None);

        var gf = await matchRepo.GetByIdAsync(matches["GF"].Id);
        var lf = await matchRepo.GetByIdAsync(matches["LF"].Id);
        Assert.Equal(t1, gf!.TeamAId);
        Assert.Equal(t2, lf!.TeamAId);

        // Jogo L1: Team 3 vs Team 4 (Bo3). Team 3 vence.
        // L1 Winner -> LF (Team B)
        var l1 = matches["L1"];
        await walkoverHandler.Handle(new RecordWalkoverCommand(l1.Id, t3), CancellationToken.None);

        lf = await matchRepo.GetByIdAsync(matches["LF"].Id);
        Assert.Equal(t3, lf!.TeamBId);

        // Jogo LF: Team 2 vs Team 3 (Bo3). Team 2 vence.
        // LF Winner -> GF (Team B)
        await walkoverHandler.Handle(new RecordWalkoverCommand(lf.Id, t2), CancellationToken.None);

        gf = await matchRepo.GetByIdAsync(matches["GF"].Id);
        Assert.Equal(t2, gf!.TeamBId);

        // Grande Final (GF): Team 1 vs Team 2 (Bo5). Team 1 vence.
        await walkoverHandler.Handle(new RecordWalkoverCommand(gf.Id, t1), CancellationToken.None);

        var updatedTournament = await tournamentRepo.GetByIdAsync(tournament.Id);
        Assert.Equal(TournamentStatus.Completed, updatedTournament!.Status);
        Assert.Equal(t1, updatedTournament.ChampionId);
        Assert.Equal("Team 1", updatedTournament.ChampionName);
    }

    [Fact]
    public async Task Rollback_WhenDownstreamMatchHasGames_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var walkoverHandler = new RecordWalkoverHandler(matchRepo, progressionService);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var rollbackHandler = new RollbackMatchSeriesHandler(matchRepo, progressionService);

        var tournament = new Tournament("Rollback Test Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var t4 = Guid.NewGuid();

        tournament.AddTeam(t1, "Team 1");
        tournament.AddTeam(t2, "Team 2");
        tournament.AddTeam(t3, "Team 3");
        tournament.AddTeam(t4, "Team 4");
        await tournamentRepo.AddAsync(tournament);

        var startHandler = new StartTournamentHandler(tournamentRepo, matchRepo);
        await startHandler.Handle(new StartTournamentCommand(tournament.Id), CancellationToken.None);

        var matches = (await matchRepo.GetByTournamentIdAsync(tournament.Id)).ToDictionary(m => m.SlotCode);

        // U1 conclui (T1 vence T2) -> T1 vai para GF, T2 vai para LF
        var u1 = matches["U1"];
        await walkoverHandler.Handle(new RecordWalkoverCommand(u1.Id, t1), CancellationToken.None);

        // Agora L1 conclui (T3 vence T4) -> T3 vai para LF
        var l1 = matches["L1"];
        await walkoverHandler.Handle(new RecordWalkoverCommand(l1.Id, t3), CancellationToken.None);

        // LF (T2 vs T3) joga 1 jogo!
        var lf = await matchRepo.GetByIdAsync(matches["LF"].Id);
        await replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(lf!.Id, 1, t2, 2400, 30, 18, 9991, null)), CancellationToken.None);

        // Tentativa de rollback de U1 deve ser bloqueada porque LF (downstream) já tem 1 jogo disputado!
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            rollbackHandler.Handle(new RollbackMatchSeriesCommand(u1.Id), CancellationToken.None));

        Assert.Contains("Não é possível reverter a partida", ex.Message);
        Assert.Contains("já possui mapas jogados", ex.Message);
    }

    [Fact]
    public async Task RemoveLastGameHandler_When1x1AndGame3Mistake_ShouldPreserveFirstTwoGames()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var removeLastHandler = new RemoveLastGameHandler(matchRepo, progressionService);

        var series = new MatchSeries(Guid.NewGuid(), "G_1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "OG", teamB, "Liquid");
        await matchRepo.AddAsync(series);

        // Jogo 1: Team A vence (1-0)
        await replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 1, teamA, 2000, 25, 15, 1001, null)), CancellationToken.None);
        // Jogo 2: Team B vence (1-1)
        await replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 2, teamB, 2200, 18, 30, 1002, null)), CancellationToken.None);

        // Jogo 3: Lançado por engano para Team A (ficando 2-1 finalizado para Team A)
        await replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 3, teamA, 2400, 32, 20, 1003, null)), CancellationToken.None);

        var updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.Equal(MatchSeriesStatus.Completed, updated!.Status);
        Assert.Equal(teamA, updated.WinnerTeamId);

        // Desfaz APENAS o jogo 3
        await removeLastHandler.Handle(new RemoveLastGameCommand(series.Id), CancellationToken.None);

        updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.Equal(MatchSeriesStatus.InProgress, updated!.Status);
        Assert.Equal(1, updated.ScoreTeamA);
        Assert.Equal(1, updated.ScoreTeamB);
        Assert.Null(updated.WinnerTeamId);
        Assert.Equal(2, updated.Games.Count); // Jogos 1 e 2 intactos!

        // Lança o jogo 3 correto para o Team B
        await replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 3, teamB, 2500, 20, 35, 1004, null)), CancellationToken.None);

        updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.Equal(MatchSeriesStatus.Completed, updated!.Status);
        Assert.Equal(1, updated.ScoreTeamA);
        Assert.Equal(2, updated.ScoreTeamB);
        Assert.Equal(teamB, updated.WinnerTeamId);
    }

    [Fact]
    public async Task RemoveLastGameHandler_WhenPlayoffsSeriesCompleted_ShouldUndoBracketAdvancement()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var removeLastHandler = new RemoveLastGameHandler(matchRepo, progressionService);

        var tournament = new Tournament("Playoff Undo Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var t4 = Guid.NewGuid();

        tournament.AddTeam(t1, "Team 1");
        tournament.AddTeam(t2, "Team 2");
        tournament.AddTeam(t3, "Team 3");
        tournament.AddTeam(t4, "Team 4");
        await tournamentRepo.AddAsync(tournament);

        var startHandler = new StartTournamentHandler(tournamentRepo, matchRepo);
        await startHandler.Handle(new StartTournamentCommand(tournament.Id), CancellationToken.None);

        var matches = (await matchRepo.GetByTournamentIdAsync(tournament.Id)).ToDictionary(m => m.SlotCode);
        var u1 = matches["U1"];

        // Team 1 vence U1 por 2x0 (Bo3) -> Team 1 promovido para GF (Team A), Team 2 para LF (Team A)
        await replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(u1.Id, 1, t1, 2000, 20, 10, 1001, null)), CancellationToken.None);
        await replayHandler.Handle(new RecordReplayResultCommand(new MatchResultIngestionDto(u1.Id, 2, t1, 2100, 22, 12, 1002, null)), CancellationToken.None);

        var gf = await matchRepo.GetByIdAsync(matches["GF"].Id);
        var lf = await matchRepo.GetByIdAsync(matches["LF"].Id);
        Assert.Equal(t1, gf!.TeamAId);
        Assert.Equal(t2, lf!.TeamAId);

        // Desfaz o jogo 2 de U1
        await removeLastHandler.Handle(new RemoveLastGameCommand(u1.Id), CancellationToken.None);

        var u1Updated = await matchRepo.GetByIdAsync(u1.Id);
        Assert.Equal(MatchSeriesStatus.InProgress, u1Updated!.Status);
        Assert.Equal(1, u1Updated.ScoreTeamA);
        Assert.Equal(0, u1Updated.ScoreTeamB);
        Assert.Null(u1Updated.WinnerTeamId);

        // Confirma que os times promovidos foram limpos das próximas fases
        gf = await matchRepo.GetByIdAsync(matches["GF"].Id);
        lf = await matchRepo.GetByIdAsync(matches["LF"].Id);
        Assert.Null(gf!.TeamAId);
        Assert.Null(lf!.TeamAId);
    }

    [Fact]
    public async Task RecordReplayResultHandler_ShouldUpdateStatsAndRecalculateWinnerCorrectly_WhenReingestingGame()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);

        var series = new MatchSeries(Guid.NewGuid(), "G_1", SeriesFormat.Bo3);
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        series.AssignTeams(teamA, "OG", teamB, "Liquid");
        await matchRepo.AddAsync(series);

        // Jogo 1: Team A vence (1-0), com duração 1500 e 20 kills
        await replayHandler.Handle(
            new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 1, teamA, 1500, 20, 10, 1001, null)),
            CancellationToken.None);

        // Atualiza apenas os metadados do jogo 1 (corrige para 2500 duração e 25 kills)
        await replayHandler.Handle(
            new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 1, teamA, 2500, 25, 12, 1001, null)),
            CancellationToken.None);

        var updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.Equal(1, updated!.Games.First().GameNumber);
        Assert.Equal(2500, updated.Games.First().DurationSeconds);
        Assert.Equal(25, updated.Games.First().TeamAKills);
        Assert.Equal(1, updated.ScoreTeamA);

        // Atualiza mudando o vencedor do jogo 1 para Team B
        await replayHandler.Handle(
            new RecordReplayResultCommand(new MatchResultIngestionDto(series.Id, 1, teamB, 2500, 25, 30, 1002, null)),
            CancellationToken.None);

        updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.Equal(0, updated!.ScoreTeamA);
        Assert.Equal(1, updated.ScoreTeamB);
        Assert.Equal(teamB, updated.Games.First().WinnerTeamId);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenValidReplay_ShouldIngestGameAndAdvanceSeries()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Dota ParseGo Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);
        await tournamentRepo.AddAsync(tournament);

        var draft = new DraftSession(tournament.Id, 4);
        var teamA = draft.Teams.First();
        var teamB = draft.Teams.Skip(1).First();

        // 5 jogadores do Time A (SteamID64 76561198000000001 a 76561198000000005)
        for (int i = 1; i <= 5; i++)
        {
            var pId = Guid.NewGuid();
            var steam64 = (76561198000000000L + i).ToString();
            draft.AddPlayerToPool(pId, $"PlayerA_{i}", steam64);
            teamA.AddStarter(new DraftPlayer(pId, $"PlayerA_{i}", steam64));
        }

        // 5 jogadores do Time B (SteamID64 76561198000000011 a 76561198000000015)
        for (int i = 1; i <= 5; i++)
        {
            var pId = Guid.NewGuid();
            var steam64 = (76561198000000010L + i).ToString();
            draft.AddPlayerToPool(pId, $"PlayerB_{i}", steam64);
            teamB.AddStarter(new DraftPlayer(pId, $"PlayerB_{i}", steam64));
        }
        await draftRepo.AddAsync(draft);

        var series = new MatchSeries(tournament.Id, "U1", SeriesFormat.Bo3);
        series.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        await matchRepo.AddAsync(series);

        // Mock ParseGoDota Output
        var mockParser = new TestDotaReplayParserService();
        var radiantPlayers = new List<ParseGoDotaPlayerDto>();
        for (int i = 1; i <= 5; i++)
        {
            radiantPlayers.Add(new ParseGoDotaPlayerDto
            {
                SteamId = 76561198000000000L + i,
                Nome = $"PlayerA_{i}",
                HeroId = i,
                Kills = 8 + i,
                Deaths = 4,
                Assists = 10,
                NetWorth = 18000,
                Gpm = 550,
                Xpm = 650
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
                Kills = 12 + i,
                Deaths = 3,
                Assists = 14,
                NetWorth = 26000,
                Gpm = 750,
                Xpm = 850
            });
        }

        mockParser.ResponseToReturn = new ParseGoDotaOutputDto
        {
            Sumario = new ParseGoDotaSummaryDto
            {
                MatchId = 8817940931,
                DuracaoSegundos = 2946.3,
                Vencedor = "Dire",
                PlacarRadiant = 45,
                PlacarDire = 53,
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
        var result = await handler.Handle(
            new UploadMatchReplayCommand(series.Id, dummyStream, "match_8817940931.dem"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(8817940931, result.DotaMatchId);
        Assert.Equal(teamB.Id, result.WinnerTeamId);
        Assert.Equal(teamB.Name, result.WinnerTeamName);
        Assert.Equal(1, result.GameNumber);
        Assert.Equal(0, result.ScoreTeamA);
        Assert.Equal(1, result.ScoreTeamB);
        Assert.Equal("InProgress", result.Status);

        var updated = await matchRepo.GetByIdAsync(series.Id);
        Assert.NotNull(updated);
        Assert.Single(updated.Games);
        Assert.Equal(teamB.Id, updated.Games.First().WinnerTeamId);
        Assert.Equal(8817940931, updated.Games.First().DotaMatchId);
        Assert.Equal(10, updated.Games.First().PlayerStats.Count);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenRosterDoesNotMatch_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Dota ParseGo Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);
        await tournamentRepo.AddAsync(tournament);

        var draft = new DraftSession(tournament.Id, 4);
        var teamA = draft.Teams.First();
        var teamB = draft.Teams.Skip(1).First();

        for (int i = 1; i <= 5; i++)
        {
            draft.AddPlayerToPool(Guid.NewGuid(), $"PlayerA_{i}", $"7656119800000000{i}");
            teamA.AddStarter(new DraftPlayer(Guid.NewGuid(), $"PlayerA_{i}", $"7656119800000000{i}"));

            draft.AddPlayerToPool(Guid.NewGuid(), $"PlayerB_{i}", $"7656119800000001{i}");
            teamB.AddStarter(new DraftPlayer(Guid.NewGuid(), $"PlayerB_{i}", $"7656119800000001{i}"));
        }
        await draftRepo.AddAsync(draft);

        var series = new MatchSeries(tournament.Id, "U1", SeriesFormat.Bo3);
        series.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        await matchRepo.AddAsync(series);

        var mockParser = new TestDotaReplayParserService();
        var radiantPlayers = new List<ParseGoDotaPlayerDto>();
        for (int i = 1; i <= 4; i++)
        {
            radiantPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 76561198000000000L + i });
        }
        radiantPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 99999999999999999L }); // Intruso

        var direPlayers = new List<ParseGoDotaPlayerDto>();
        for (int i = 1; i <= 5; i++)
        {
            direPlayers.Add(new ParseGoDotaPlayerDto { SteamId = 76561198000000010L + i });
        }

        mockParser.ResponseToReturn = new ParseGoDotaOutputDto
        {
            Sumario = new ParseGoDotaSummaryDto
            {
                MatchId = 12345,
                Vencedor = "Radiant",
                DataHoraInicio = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")
            },
            JogadoresEEventos = new ParseGoDotaPlayersAndEventsDto { Radiant = radiantPlayers, Dire = direPlayers }
        };

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, mockParser, sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new UploadMatchReplayCommand(series.Id, dummyStream, "invalid.dem"), CancellationToken.None));

        Assert.Contains("não corresponde aos times inscritos", ex.Message);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenReplayIsDuplicateInTournament_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Dota Duplicate Check Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);
        await tournamentRepo.AddAsync(tournament);

        var draft = new DraftSession(tournament.Id, 4);
        var teamA = draft.Teams.First();
        var teamB = draft.Teams.Skip(1).First();

        for (int i = 1; i <= 5; i++)
        {
            draft.AddPlayerToPool(Guid.NewGuid(), $"PlayerA_{i}", $"7656119800000000{i}");
            teamA.AddStarter(new DraftPlayer(Guid.NewGuid(), $"PlayerA_{i}", $"7656119800000000{i}"));

            draft.AddPlayerToPool(Guid.NewGuid(), $"PlayerB_{i}", $"7656119800000001{i}");
            teamB.AddStarter(new DraftPlayer(Guid.NewGuid(), $"PlayerB_{i}", $"7656119800000001{i}"));
        }
        await draftRepo.AddAsync(draft);

        // Cria uma série que JÁ possui um jogo gravado com o MatchId 999888
        var existingSeries = new MatchSeries(tournament.Id, "U1", SeriesFormat.Bo3);
        existingSeries.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        existingSeries.RecordGameReplay(teamA.Id, 1800, 20, 10, 999888, null);
        await matchRepo.AddAsync(existingSeries);

        // Tenta enviar o MESMO MatchId 999888 para uma nova série
        var newSeries = new MatchSeries(tournament.Id, "U2", SeriesFormat.Bo3);
        newSeries.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        await matchRepo.AddAsync(newSeries);

        var mockParser = new TestDotaReplayParserService();
        mockParser.ResponseToReturn = new ParseGoDotaOutputDto
        {
            Sumario = new ParseGoDotaSummaryDto
            {
                MatchId = 999888, // MESMO MatchId!
                Vencedor = "Radiant",
                DataHoraInicio = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")
            },
            JogadoresEEventos = new ParseGoDotaPlayersAndEventsDto
            {
                Radiant = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto { SteamId = 76561198000000000L + i }).ToList(),
                Dire = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto { SteamId = 76561198000000010L + i }).ToList()
            }
        };

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, mockParser, sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new UploadMatchReplayCommand(newSeries.Id, dummyStream, "match.dem"), CancellationToken.None));

        Assert.Contains("já foi utilizada em outro confronto deste campeonato", ex.Message);
    }

    [Fact]
    public async Task UploadMatchReplayHandler_WhenReplayDateIsBeforeTournamentCreation_ShouldThrowDomainException()
    {
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var draftRepo = new InMemoryDraftSessionRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);
        var replayHandler = new RecordReplayResultHandler(matchRepo, progressionService);
        var sender = new TestSender(req => replayHandler.Handle((RecordReplayResultCommand)req, CancellationToken.None));

        var tournament = new Tournament("Dota Timestamp Cup",
            TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo1, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!);
        await tournamentRepo.AddAsync(tournament);

        var draft = new DraftSession(tournament.Id, 4);
        var teamA = draft.Teams.First();
        var teamB = draft.Teams.Skip(1).First();

        for (int i = 1; i <= 5; i++)
        {
            draft.AddPlayerToPool(Guid.NewGuid(), $"PlayerA_{i}", $"7656119800000000{i}");
            teamA.AddStarter(new DraftPlayer(Guid.NewGuid(), $"PlayerA_{i}", $"7656119800000000{i}"));

            draft.AddPlayerToPool(Guid.NewGuid(), $"PlayerB_{i}", $"7656119800000001{i}");
            teamB.AddStarter(new DraftPlayer(Guid.NewGuid(), $"PlayerB_{i}", $"7656119800000001{i}"));
        }
        await draftRepo.AddAsync(draft);

        var series = new MatchSeries(tournament.Id, "U1", SeriesFormat.Bo3);
        series.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
        await matchRepo.AddAsync(series);

        var mockParser = new TestDotaReplayParserService();
        mockParser.ResponseToReturn = new ParseGoDotaOutputDto
        {
            Sumario = new ParseGoDotaSummaryDto
            {
                MatchId = 777666,
                Vencedor = "Radiant",
                DataHoraInicio = "10/01/2020 15:30:00" // Data bem antiga, muito antes da criação do torneio!
            },
            JogadoresEEventos = new ParseGoDotaPlayersAndEventsDto
            {
                Radiant = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto { SteamId = 76561198000000000L + i }).ToList(),
                Dire = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto { SteamId = 76561198000000010L + i }).ToList()
            }
        };

        var handler = new UploadMatchReplayHandler(matchRepo, draftRepo, tournamentRepo, mockParser, sender);
        using var dummyStream = new MemoryStream(new byte[1024]);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new UploadMatchReplayCommand(series.Id, dummyStream, "match.dem"), CancellationToken.None));

        Assert.Contains("antes da criação/início do campeonato", ex.Message);
    }

    [Fact]
    public async Task GoCliReplayParserService_WhenStreamHasInvalidSignature_ShouldThrowDomainException()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Tournaments.Infrastructure.ReplayParsing.GoCliReplayParserService>.Instance;
        var service = new Tournaments.Infrastructure.ReplayParsing.GoCliReplayParserService(config, logger);

        // 600 KB com bytes aleatórios (sem PBDEMS2)
        var invalidBytes = new byte[600 * 1024];
        new Random(42).NextBytes(invalidBytes);
        using var stream = new MemoryStream(invalidBytes);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.ParseReplayAsync(stream, "fake_replay.dem", CancellationToken.None));

        Assert.Contains("assinatura PBDEMS2 não encontrada", ex.Message);
    }

    [Fact]
    public async Task GoCliReplayParserService_WhenStreamTooSmall_ShouldThrowDomainException()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Tournaments.Infrastructure.ReplayParsing.GoCliReplayParserService>.Instance;
        var service = new Tournaments.Infrastructure.ReplayParsing.GoCliReplayParserService(config, logger);

        using var smallStream = new MemoryStream(new byte[100]); // apenas 100 bytes

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.ParseReplayAsync(smallStream, "small.dem", CancellationToken.None));

        Assert.Contains("muito pequeno", ex.Message);
    }
}

public class TestDotaReplayParserService : IDotaReplayParserService
{
    public ParseGoDotaOutputDto? ResponseToReturn { get; set; }

    public Task<ParseGoDotaOutputDto> ParseReplayAsync(Stream replayStream, string originalFileName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ResponseToReturn ?? new ParseGoDotaOutputDto());
    }
}

public class TestSender : ISender
{
    private readonly Func<object, Task> _handler;

    public TestSender(Func<object, Task> handler)
    {
        _handler = handler;
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
    {
        await _handler(request!);
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        await _handler(request);
        return default!;
    }

    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        await _handler(request);
        return null;
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}


