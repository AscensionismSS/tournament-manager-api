using System.Security.Claims;
using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Tournaments.Application.Common.Services;
using Tournaments.Application.Features.Draft.RenameTeam;
using Tournaments.Application.Features.Matches.RollbackMatchSeries;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Services;
using Tournaments.Domain.ValueObjects;
using Tournaments.Infrastructure.Repositories;
using TournamentCreator.WebApi.Hubs.v1;
using Xunit;

namespace Tournaments.UnitTests;

public class NewFixesUnitTests
{
    [Fact]
    public void MatchSeries_UpdateGameFromReplay_Bo2_When1x1Tie_ShouldMaintainCompletedStatus()
    {
        // 1. Arrange: Cria uma série Bo2 com 2 equipes
        var tournamentId = Guid.NewGuid();
        var series = new MatchSeries(tournamentId, "G1-M1", SeriesFormat.Bo2);
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        series.AssignTeams(teamAId, "Team Spirit", teamBId, "Team Liquid");

        // Registra jogo 1 (Team A vence)
        series.RecordGameReplay(teamAId, 2100, 30, 15);
        Assert.Equal(MatchSeriesStatus.InProgress, series.Status);

        // Registra jogo 2 (Team A vence -> 2x0)
        series.RecordGameReplay(teamAId, 1800, 25, 10);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Equal(teamAId, series.WinnerTeamId);

        // 2. Act: Atualiza o jogo 2 para que Team B seja o vencedor (placar 1x1)
        series.UpdateGameFromReplay(
            gameNumber: 2,
            winnerTeamId: teamBId,
            durationSeconds: 2400,
            teamAKills: 15,
            teamBKills: 35,
            dotaMatchId: 12345,
            playerStats: null
        );

        // 3. Assert: Placar agora é 1x1, série DEVE permanecer Completed (e não regredir para InProgress!)
        Assert.Equal(1, series.ScoreTeamA);
        Assert.Equal(1, series.ScoreTeamB);
        Assert.Equal(MatchSeriesStatus.Completed, series.Status);
        Assert.Null(series.WinnerTeamId);
        Assert.Null(series.LoserTeamId);
    }

    [Fact]
    public async Task Rollback_GroupStageMatch_WhenPlayoffsStarted_ShouldThrowDomainException()
    {
        // 1. Arrange: Torneio com formato de Fase de Grupos + Playoffs
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();
        var progressionService = new BracketProgressionService(tournamentRepo, matchRepo);

        var tournament = new Tournament(
            "TI 2026",
            TournamentFormatConfig.Create(TournamentType.GroupStageAndPlayoffs, PlayoffFormat.DoubleElimination, 4).Value!,
            SeriesConfig.Create(SeriesFormat.Bo2, SeriesFormat.Bo3, SeriesFormat.Bo5).Value!
        );

        // Adiciona 4 equipes
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var t4 = Guid.NewGuid();
        tournament.AddTeam(t1, "T1");
        tournament.AddTeam(t2, "T2");
        tournament.AddTeam(t3, "T3");
        tournament.AddTeam(t4, "T4");

        tournament.Start(); // Entra em GroupStage
        await tournamentRepo.AddAsync(tournament);

        // Cria partida de grupo G1-M1
        var groupSeries = new MatchSeries(tournament.Id, "G1-M1", SeriesFormat.Bo2);
        groupSeries.AssignTeams(t1, "T1", t2, "T2");
        groupSeries.RecordGameReplay(t1, 2000, 20, 10);
        groupSeries.RecordGameReplay(t1, 2000, 20, 10);
        await matchRepo.AddAsync(groupSeries);

        // Avança o torneio para Playoffs
        tournament.GeneratePlayoffs();
        await tournamentRepo.UpdateAsync(tournament);
        Assert.Equal(TournamentStatus.Playoffs, tournament.Status);

        // 2. Act & Assert: Tentar reverter a partida de grupo enquanto nos Playoffs deve falhar
        var rollbackHandler = new RollbackMatchSeriesHandler(matchRepo, progressionService);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            rollbackHandler.Handle(new RollbackMatchSeriesCommand(groupSeries.Id), CancellationToken.None)
        );

        Assert.Contains("Playoffs já foi iniciada ou concluída", ex.Message);
    }

    [Fact]
    public void DraftSession_AdminCanRenameTeam_AtAnyTime_And_EmptyNameDefaultsToStandardName()
    {
        // 1. Arrange: Sessão em estado inicial (SelectingCaptains)
        var session = new DraftSession(Guid.NewGuid(), 4);
        var team1 = session.Teams.First();

        // 2. Act 1: Admin renomeia a equipe na fase inicial de seleção de capitães
        session.RenameTeam(team1.Id, "Natus Vincere", session.DraftVersion, actingUserId: null, isAdmin: true);

        // Assert 1: Nome alterado com sucesso sem exigir estado Completed ou 5 jogadores
        Assert.Equal("Natus Vincere", team1.Name);

        // 3. Act 2: Admin renomeia passando string vazia/nula -> deve aplicar o nome padrão e nunca ficar nulo!
        session.RenameTeam(team1.Id, "", session.DraftVersion, actingUserId: null, isAdmin: true);

        // Assert 2: Nome padrão restaurado ("Time 1")
        Assert.False(string.IsNullOrWhiteSpace(team1.Name));
        Assert.Equal("Time 1", team1.Name);

        // 4. Act 3: Não-admin (capitão) tenta renomear durante o draft -> deve ser bloqueado
        var ex = Assert.Throws<DomainException>(() =>
            session.RenameTeam(team1.Id, "Illegal Name", session.DraftVersion, actingUserId: Guid.NewGuid().ToString(), isAdmin: false)
        );
        Assert.Contains("Apenas administradores do torneio têm permissão", ex.Message);
    }

    [Fact]
    public void DraftSession_RenameTeam_WhenNonAdminWithoutActingUserId_ShouldThrowDomainException()
    {
        // 1. Arrange: Adiciona jogadores à pool na fase SelectingCaptains
        var session = new DraftSession(Guid.NewGuid(), 4);
        var team1 = session.Teams.First();
        var captainId = Guid.NewGuid();
        session.AddPlayerToPool(captainId, "Captain 1");

        var otherCaptainIds = new List<Guid>();
        for (int i = 1; i < 4; i++)
        {
            var pId = Guid.NewGuid();
            otherCaptainIds.Add(pId);
            session.AddPlayerToPool(pId, $"Captain {i + 1}");
        }

        var starterIds = new List<Guid>();
        for (int i = 0; i < 16; i++)
        {
            var pId = Guid.NewGuid();
            starterIds.Add(pId);
            session.AddPlayerToPool(pId, $"Player {i}");
        }

        // Seleciona capitães
        session.SelectCaptain(captainId, session.DraftVersion, team1.Id);
        for (int i = 1; i < 4; i++)
        {
            session.SelectCaptain(otherCaptainIds[i - 1], session.DraftVersion, session.Teams.ElementAt(i).Id);
        }

        session.FinalizeCaptainOrder(PickOrderType.Manual, session.Teams.Select(t => t.Id).ToList(), session.DraftVersion);

        // Escolhe os titulares para todos (16 picks)
        foreach (var pId in starterIds)
        {
            session.PickStarterPlayer(pId, session.DraftVersion);
        }

        session.CompleteDraft(session.DraftVersion);

        // 2. Act & Assert: Não-admin com actingUserId nulo/vazio DEVE falhar (não pode contornar segurança)
        var ex = Assert.Throws<DomainException>(() =>
            session.RenameTeam(team1.Id, "Hacker Team", session.DraftVersion, actingUserId: null, isAdmin: false)
        );
        Assert.Contains("Apenas administradores do torneio têm permissão", ex.Message);
    }

    [Fact]
    public void DraftSession_RenameTeam_WhenDefaultNameCollidesWithExistingTeam_ShouldFindNextAvailableDefaultName()
    {
        // 1. Arrange: Sessão com 4 equipes
        var session = new DraftSession(Guid.NewGuid(), 4);
        var teamA = session.Teams.ElementAt(0); // Name: "Time 1", PickOrder: 0
        var teamB = session.Teams.ElementAt(1); // Name: "Time 2", PickOrder: 1

        // Renomeia Time A para "Secret"
        session.RenameTeam(teamA.Id, "Team Secret", session.DraftVersion, actingUserId: null, isAdmin: true);
        Assert.Equal("Team Secret", teamA.Name);

        // Modifica PickOrder (Time A vira PickOrder 1, Time B continua com Name "Time 2")
        teamA.PickOrder = 1;

        // 2. Act: Admin renomeia Time A com string vazia -> o preferred seria "Time 2", que já existe no Time B!
        session.RenameTeam(teamA.Id, "", session.DraftVersion, actingUserId: null, isAdmin: true);

        // 3. Assert: Sistema evita colisão e acha um nome livre como "Time 1" ou subsequente, nunca nulo!
        Assert.False(string.IsNullOrWhiteSpace(teamA.Name));
        Assert.NotEqual("Time 2", teamA.Name); // Não pode colidir com o Time B!
        Assert.Contains("Time", teamA.Name);
    }

    [Fact]
    public void Controllers_AuthorizationAttributes_EnforceSuperAdminExclusivity()
    {
        // Verifica restrições no AuthController para recovery-requests e unlock-steam
        var authMethods = typeof(TournamentCreator.WebApi.Controllers.v1.AuthController).GetMethods();
        var recoveryReqMethod = authMethods.First(m => m.Name == "GetRecoveryRequests");
        var approveSteamMethod = authMethods.First(m => m.Name == "ApproveSteamUnlock");
        var rejectSteamMethod = authMethods.First(m => m.Name == "RejectSteamUnlock");
        var unlockSteamMethod = authMethods.First(m => m.Name == "UnlockSteam");

        var recAuth = recoveryReqMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().First();
        Assert.Equal("SuperAdmin", recAuth.Roles);

        var appAuth = approveSteamMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().First();
        Assert.Equal("SuperAdmin", appAuth.Roles);

        var rejAuth = rejectSteamMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().First();
        Assert.Equal("SuperAdmin", rejAuth.Roles);

        var unlAuth = unlockSteamMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().First();
        Assert.Equal("SuperAdmin", unlAuth.Roles);

        // Verifica restrições no DraftController para operações do draft
        var draftMethods = typeof(TournamentCreator.WebApi.Controllers.v1.DraftController).GetMethods();
        string[] draftAdminEndpoints = [
            "AddPlayerToPool",
            "AddLatePlayer",
            "SelectCaptain",
            "RemoveCaptain",
            "AdvanceToOrderSelection",
            "FinalizeCaptainOrder",
            "PickStarterPlayer",
            "StartSubstitution",
            "ConfirmSubstitution",
            "CancelSubstitution",
            "AdvanceToSubstitutes",
            "AllocateReserve",
            "CompleteDraft"
        ];

        foreach (var endpointName in draftAdminEndpoints)
        {
            var method = draftMethods.First(m => m.Name == endpointName);
            var auth = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().FirstOrDefault();

            Assert.NotNull(auth);
            Assert.Equal("TournamentAdmin,SuperAdmin", auth.Roles);
        }
    }

    [Fact]
    public void GroupStandingsEvaluator_ComplexMultiWayTie_PreservesTransitivity_NoException()
    {
        // Cria 5 equipes com pontuações variadas e confrontos cruzados
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var id4 = Guid.NewGuid();
        var id5 = Guid.NewGuid();

        var team1 = new TeamStanding(id1, "Team 1", 6, 4, 2, 4000, 50, 40, new Dictionary<Guid, int> { [id2] = 2, [id3] = 0, [id4] = 1, [id5] = 1 });
        var team2 = new TeamStanding(id2, "Team 2", 6, 4, 2, 3500, 55, 35, new Dictionary<Guid, int> { [id1] = 0, [id3] = 2, [id4] = 1, [id5] = 1 });
        var team3 = new TeamStanding(id3, "Team 3", 6, 4, 2, 3000, 60, 30, new Dictionary<Guid, int> { [id1] = 2, [id2] = 0, [id4] = 1, [id5] = 1 });
        var team4 = new TeamStanding(id4, "Team 4", 3, 2, 4, 2500, 30, 45, new Dictionary<Guid, int> { [id1] = 1, [id2] = 1, [id3] = 1, [id5] = 2 });
        var team5 = new TeamStanding(id5, "Team 5", 3, 2, 4, 2800, 25, 50, new Dictionary<Guid, int> { [id1] = 1, [id2] = 1, [id3] = 1, [id4] = 0 });

        // Act: Ordena a tabela sem estourar ArgumentException de transitividade do IntroSort
        var sorted = GroupStandingsEvaluator.SortStandings([team5, team4, team3, team2, team1]);

        Assert.Equal(5, sorted.Count);
        // Os 3 primeiros têm 6 pontos, os 2 últimos têm 3 pontos
        Assert.Equal(6, sorted[0].Points);
        Assert.Equal(6, sorted[1].Points);
        Assert.Equal(6, sorted[2].Points);
        Assert.Equal(3, sorted[3].Points);
        Assert.Equal(3, sorted[4].Points);
    }

    [Fact]
    public async Task TournamentDraftHub_JoinUserChannel_WhenUserMismatchAndNotSuperAdmin_ShouldThrowHubException()
    {
        // 1. Arrange: Hub com contexto de usuário comum autenticado
        var hub = new TournamentDraftHub(NullLogger<TournamentDraftHub>.Instance);

        var loggedInUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, loggedInUserId.ToString()),
            new Claim(ClaimTypes.Role, "Player")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        hub.Context = new TestHubCallerContext(principal);

        var targetVictimUserId = Guid.NewGuid();

        // 2. Act & Assert: Tentar assinar o canal pessoal de outro usuário deve ser bloqueado com HubException
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinUserChannel(userId: targetVictimUserId));
        Assert.Contains("Acesso negado", ex.Message);
    }

    [Fact]
    public async Task TournamentDraftHub_JoinUserChannel_WhenSuperAdmin_ShouldAllow()
    {
        // 1. Arrange: Hub com contexto de SuperAdmin
        var hub = new TournamentDraftHub(NullLogger<TournamentDraftHub>.Instance);

        var loggedInUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, loggedInUserId.ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var fakeGroups = new FakeGroupManager();
        hub.Context = new TestHubCallerContext(principal);
        hub.Groups = fakeGroups;

        var targetUserId = Guid.NewGuid();

        // 2. Act: SuperAdmin pode assinar canal de qualquer usuário para fins de auditoria/suporte
        await hub.JoinUserChannel(targetUserId);

        Assert.Contains(fakeGroups.AddedGroups, g => g.GroupName == $"user-{targetUserId}");
    }

    private sealed class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = [];
        public List<(string ConnectionId, string GroupName)> RemovedGroups { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        public override string ConnectionId => "conn-123";
        public override string? UserIdentifier => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public TestHubCallerContext(ClaimsPrincipal? user)
        {
            User = user;
        }

        public override void Abort() { }
    }

    [Fact]
    public void ParseGoDotaJson_Deserialization_ShouldSucceedForAllGeneratedJsons()
    {
        var jsonFiles = Directory.GetFiles(@"C:\Users\gabri\Desktop\TournamentCreator\convertido", "*.json");
        Assert.NotEmpty(jsonFiles);

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        };

        foreach (var file in jsonFiles)
        {
            var content = File.ReadAllText(file);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<Tournaments.Contracts.Replay.ParseGoDotaOutputDto>(content, options);

            Assert.NotNull(parsed);
            Assert.NotNull(parsed.Sumario);
            Assert.NotNull(parsed.JogadoresEEventos);
            Assert.NotEmpty(parsed.JogadoresEEventos.Radiant);
            Assert.NotEmpty(parsed.JogadoresEEventos.Dire);
        }
    }
    
    [Fact]
    public async Task ParseReplayAsync_ShouldSucceedForDownloadsReplays()
    {
        var parserService = new Tournaments.Infrastructure.ReplayParsing.GoCliReplayParserService(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Tournaments.Infrastructure.ReplayParsing.GoCliReplayParserService>.Instance
        );

        var downloads = new[]
        {
            @"C:\Users\gabri\Downloads\9065043011 - Mateus.dem",
            @"C:\Users\gabri\Downloads\9065060478 - Mateus.dem",
            @"C:\Users\gabri\Downloads\9065075876 - Mateus.dem"
        };

        foreach (var path in downloads)
        {
            if (!File.Exists(path)) continue;
            using var stream = File.OpenRead(path);
            var result = await parserService.ParseReplayAsync(stream, Path.GetFileName(path));
            Assert.NotNull(result);
            Assert.NotNull(result.Sumario);
            Assert.NotNull(result.JogadoresEEventos);
            Assert.True(result.Sumario.MatchId > 0);
        }
    }

}
