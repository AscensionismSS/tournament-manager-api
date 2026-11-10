using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.ValueObjects;
using Xunit;

namespace Tournaments.UnitTests;

public class EndToEndTournamentSimulationTests
{
    [Fact]
    public void SimulateDraftSession_CompleteFlow_WithSubstitutionAndReserves()
    {
        // 1. Cria sessão de draft para 4 equipes
        var tournamentId = Guid.NewGuid();
        var draft = new DraftSession(tournamentId, targetTeamCount: 4);

        // 2. Adiciona 24 jogadores na Pool (4 capitães + 16 titulares + 4 reservas)
        var players = Enumerable.Range(1, 24)
            .Select(i => new { Id = Guid.NewGuid(), Name = $"Player_{i}" })
            .ToList();

        foreach (var p in players)
        {
            draft.AddPlayerToPool(p.Id, p.Name);
        }

        Assert.Equal(24, draft.PlayerPool.Count);
        Assert.Equal(DraftState.SelectingCaptains, draft.State);

        // 3. Admin seleciona 4 capitães
        for (int i = 0; i < 4; i++)
        {
            draft.SelectCaptain(players[i].Id, draft.DraftVersion);
        }

        Assert.Equal(4, draft.Teams.Count);
        Assert.True(draft.Teams.All(t => t.CaptainId.HasValue));
        draft.AdvanceToCaptainOrderSelection(draft.DraftVersion);
        Assert.Equal(DraftState.CaptainOrderSelection, draft.State);

        // 4. Admin finaliza ordem (Manual: 0, 1, 2, 3)
        var teamIds = draft.Teams.Select(t => t.Id).ToList();
        draft.FinalizeCaptainOrder(PickOrderType.Manual, teamIds, draft.DraftVersion);
        Assert.Equal(DraftState.DraftingStarters, draft.State);

        // 5. Executa os primeiros 3 picks do Snake Draft
        draft.PickStarterPlayer(players[4].Id, draft.DraftVersion); // Turno 0 -> Time 0
        draft.PickStarterPlayer(players[5].Id, draft.DraftVersion); // Turno 1 -> Time 1
        draft.PickStarterPlayer(players[6].Id, draft.DraftVersion); // Turno 2 -> Time 2

        // 6. Teste de Substituição: Time 2 pegou Player 6 errado, quer trocar pelo Player 7!
        var team2 = draft.Teams.First(t => t.PickOrder == 2);
        draft.StartSubstitution(players[6].Id, team2.Id, draft.DraftVersion);
        Assert.Equal(DraftState.Substituting, draft.State);

        draft.ConfirmSubstitution(players[7].Id, draft.DraftVersion);
        Assert.Equal(DraftState.DraftingStarters, draft.State);
        Assert.Contains(team2.Starters, p => p.Id == players[7].Id);
        Assert.DoesNotContain(team2.Starters, p => p.Id == players[6].Id);
        Assert.Contains(draft.PlayerPool, p => p.Id == players[6].Id);

        // 7. Continua o draft a partir do pick 3 (Turno 3 -> Time 3) até o final (16 picks)
        // Jogadores disponíveis restantes para os 13 picks restantes:
        var poolList = draft.PlayerPool.Take(13).ToList();
        foreach (var p in poolList)
        {
            draft.PickStarterPlayer(p.Id, draft.DraftVersion);
        }

        // Todos os times devem ter exatamente 5 titulares
        foreach (var team in draft.Teams)
        {
            Assert.Equal(5, team.Starters.Count);
        }

        // Admin decide avançar para a fase de reservas
        draft.AdvanceToSubstitutes(draft.DraftVersion);
        Assert.Equal(DraftState.DraftingSubstitutes, draft.State);

        // 8. Alocação de 1 Reserva para o Time 0
        var reservePlayer = draft.PlayerPool.First();
        var team0 = draft.Teams.First(t => t.PickOrder == 0);
        draft.AllocateReserve(team0.Id, reservePlayer.Id, draft.DraftVersion);
        Assert.Single(team0.Reserves);

        // 9. Conclusão do Draft
        draft.CompleteDraft(draft.DraftVersion);
        Assert.Equal(DraftState.Completed, draft.State);

        // 10. Admin renomeia uma equipe após a conclusão do Draft
        var teamToRename = draft.Teams.First();
        draft.RenameTeam(teamToRename.Id, "Natus Vincere", draft.DraftVersion);
        Assert.Equal("Natus Vincere", teamToRename.Name);
    }

    [Fact]
    public void SimulateTournament_4Teams_DoubleElimination_ToChampion()
    {
        // 1. Configuração do Torneio
        var formatConfig = TournamentFormatConfig.Create(TournamentType.DirectPlayoffs, PlayoffFormat.DoubleElimination, 4).Value;
        var seriesConfig = SeriesConfig.Create(null, SeriesFormat.Bo3, SeriesFormat.Bo5).Value;

        var tournament = new Tournament("Copa Dota Brasil", formatConfig, seriesConfig);

        var team1Id = Guid.NewGuid();
        var team2Id = Guid.NewGuid();
        var team3Id = Guid.NewGuid();
        var team4Id = Guid.NewGuid();

        tournament.AddTeam(team1Id, "Team Spirit");
        tournament.AddTeam(team2Id, "Team Liquid");
        tournament.AddTeam(team3Id, "Gaimin Gladiators");
        tournament.AddTeam(team4Id, "BetBoom Team");

        tournament.Start();
        Assert.Equal(TournamentStatus.Playoffs, tournament.Status);
        Assert.Equal(4, tournament.BracketSlots.Count);

        // 2. Simulação das Séries de Partidas (MatchSeries)
        // U1: Team 1 vs Team 2 (Bo3)
        var u1 = new MatchSeries(tournament.Id, "U1", SeriesFormat.Bo3);
        u1.AssignTeams(team1Id, "Team Spirit", team2Id, "Team Liquid");
        u1.RecordGameReplay(team1Id, 2400, 30, 15);
        u1.RecordGameReplay(team1Id, 2100, 25, 10); // Team 1 vence 2x0
        Assert.Equal(team1Id, u1.WinnerTeamId);

        // L1: Team 3 vs Team 4 (Bo3)
        var l1 = new MatchSeries(tournament.Id, "L1", SeriesFormat.Bo3);
        l1.AssignTeams(team3Id, "Gaimin Gladiators", team4Id, "BetBoom Team");
        l1.RecordGameReplay(team3Id, 1800, 28, 12);
        l1.RecordGameReplay(team3Id, 2000, 35, 20); // Team 3 vence 2x0
        Assert.Equal(team3Id, l1.WinnerTeamId);

        // LF (Lower Final): Loser(U1) [Team 2] vs Winner(L1) [Team 3] (Bo3)
        var lf = new MatchSeries(tournament.Id, "LF", SeriesFormat.Bo3);
        lf.AssignTeams(team2Id, "Team Liquid", team3Id, "Gaimin Gladiators");
        lf.RecordGameReplay(team2Id, 2200, 22, 18);
        lf.RecordGameReplay(team3Id, 2300, 24, 20);
        lf.RecordGameReplay(team2Id, 2500, 31, 25); // Team 2 vence 2x1
        Assert.Equal(team2Id, lf.WinnerTeamId);

        // GF (Grand Final): Winner(U1) [Team 1] vs Winner(LF) [Team 2] (Bo5)
        var gf = new MatchSeries(tournament.Id, "GF", SeriesFormat.Bo5);
        gf.AssignTeams(team1Id, "Team Spirit", team2Id, "Team Liquid");
        gf.RecordGameReplay(team1Id, 2100, 20, 10);
        gf.RecordGameReplay(team2Id, 2600, 25, 22);
        gf.RecordGameReplay(team1Id, 2300, 30, 15);
        gf.RecordGameReplay(team1Id, 1900, 35, 18); // Team 1 vence 3x1
        Assert.Equal(team1Id, gf.WinnerTeamId);

        // 3. Coroa o Campeão no Torneio
        tournament.CrownChampion(team1Id, "Team Spirit");

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.Equal(team1Id, tournament.ChampionId);
        Assert.Equal("Team Spirit", tournament.ChampionName);
    }
}
