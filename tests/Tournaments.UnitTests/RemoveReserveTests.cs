using BuildingBlocks.Domain;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Application.Features.Draft.RemoveReserve;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;
using Tournaments.Infrastructure.Repositories;
using Xunit;

namespace Tournaments.UnitTests;

public class RemoveReserveTests
{
    private class DummyNotifier : ITournamentRealtimeNotifier
    {
        public int NotificationCount { get; private set; }
        public Task NotifyDraftStateSnapshotAsync(Guid tournamentId, DraftStateSnapshotDto snapshot, CancellationToken ct = default)
        {
            NotificationCount++;
            return Task.CompletedTask;
        }

        public Task NotifyDraftCompletedAsync(Guid tournamentId, DraftCompletedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyPlayerPickedAsync(Guid tournamentId, PlayerPickedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionCanceledAsync(Guid tournamentId, SubstitutionCanceledEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionConfirmedAsync(Guid tournamentId, SubstitutionConfirmedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySubstitutionStartedAsync(Guid tournamentId, SubstitutionStartedEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyTournamentWebhookAsync(Tournaments.Contracts.Tournaments.TournamentWebhookEventDto evt, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void DraftTeam_RemoveReserve_WhenPlayerExists_ShouldRemoveAndReturnPlayer()
    {
        var draft = new DraftSession(Guid.NewGuid(), 4);
        var team = draft.Teams.First();
        var player = new DraftPlayer(Guid.NewGuid(), "SubPlayer");

        team.AddReserve(player);
        Assert.Single(team.Reserves);

        var removed = team.RemoveReserve(player.Id);

        Assert.Equal(player.Id, removed.Id);
        Assert.Empty(team.Reserves);
    }

    [Fact]
    public void DraftTeam_RemoveReserve_WhenPlayerNotFound_ShouldThrowDomainException()
    {
        var draft = new DraftSession(Guid.NewGuid(), 4);
        var team = draft.Teams.First();

        var ex = Assert.Throws<DomainException>(() => team.RemoveReserve(Guid.NewGuid()));
        Assert.Contains("não encontrado na equipe", ex.Message);
    }

    [Fact]
    public void DraftSession_RemoveReserve_DuringDraftingSubstitutes_ShouldRemoveFromTeamAndAddToPool()
    {
        var draft = new DraftSession(Guid.NewGuid(), 4);

        // Preenche jogadores da pool: 4 equipes * 5 titulares = 20 titulares + 2 reservas = 22 jogadores
        var poolPlayers = new List<DraftPlayer>();
        for (int i = 0; i < 22; i++)
        {
            var p = new DraftPlayer(Guid.NewGuid(), $"Player_{i}");
            poolPlayers.Add(p);
            draft.AddPlayerToPool(p.Id, p.Name);
        }

        // Seleciona capitães para as 4 equipes
        var teams = draft.Teams.ToList();
        for (int i = 0; i < 4; i++)
        {
            draft.SelectCaptain(poolPlayers[i].Id, draft.DraftVersion, teams[i].Id);
        }

        draft.AdvanceToCaptainOrderSelection(draft.DraftVersion);
        draft.FinalizeCaptainOrder(PickOrderType.Manual, teams.Select(t => t.Id).ToList(), draft.DraftVersion);

        // Executa 16 picks de titulares restantes (4 rounds * 4 times = 16 picks)
        for (int i = 4; i < 20; i++)
        {
            draft.PickStarterPlayer(poolPlayers[i].Id, draft.DraftVersion);
        }

        // Aloca jogador 20 como reserva da equipe 0
        var reservePlayer = poolPlayers[20];
        draft.AllocateReserve(teams[0].Id, reservePlayer.Id, draft.DraftVersion);
        Assert.Equal(DraftState.DraftingSubstitutes, draft.State);
        Assert.Single(teams[0].Reserves);
        Assert.DoesNotContain(draft.PlayerPool, p => p.Id == reservePlayer.Id);

        // Remove reserva
        int versionBefore = draft.DraftVersion;
        draft.RemoveReserve(teams[0].Id, reservePlayer.Id, draft.DraftVersion);

        Assert.Empty(teams[0].Reserves);
        Assert.Contains(draft.PlayerPool, p => p.Id == reservePlayer.Id);
        Assert.Equal(versionBefore + 1, draft.DraftVersion);
    }

    [Fact]
    public void DraftSession_RemoveReserve_WhenDraftCompleted_ShouldAllowRemovalAndIncrementVersion()
    {
        var draft = new DraftSession(Guid.NewGuid(), 4);

        var poolPlayers = new List<DraftPlayer>();
        for (int i = 0; i < 22; i++)
        {
            var p = new DraftPlayer(Guid.NewGuid(), $"Player_{i}");
            poolPlayers.Add(p);
            draft.AddPlayerToPool(p.Id, p.Name);
        }

        var teams = draft.Teams.ToList();
        for (int i = 0; i < 4; i++)
        {
            draft.SelectCaptain(poolPlayers[i].Id, draft.DraftVersion, teams[i].Id);
        }

        draft.AdvanceToCaptainOrderSelection(draft.DraftVersion);
        draft.FinalizeCaptainOrder(PickOrderType.Manual, teams.Select(t => t.Id).ToList(), draft.DraftVersion);

        for (int i = 4; i < 20; i++)
        {
            draft.PickStarterPlayer(poolPlayers[i].Id, draft.DraftVersion);
        }

        var reservePlayer = poolPlayers[20];
        draft.AllocateReserve(teams[0].Id, reservePlayer.Id, draft.DraftVersion);

        // Conclui draft
        draft.CompleteDraft(draft.DraftVersion);
        Assert.Equal(DraftState.Completed, draft.State);

        // Remove reserva no status Completed
        int versionBefore = draft.DraftVersion;
        draft.RemoveReserve(teams[0].Id, reservePlayer.Id, 999); // Versão relaxada quando Completed

        Assert.Empty(teams[0].Reserves);
        Assert.Contains(draft.PlayerPool, p => p.Id == reservePlayer.Id);
        Assert.Equal(versionBefore + 1, draft.DraftVersion);
    }

    [Fact]
    public async Task RemoveReserveHandler_ShouldExecuteAndNotifySnapshot()
    {
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new DummyNotifier();
        var handler = new RemoveReserveHandler(draftRepo, notifier);

        var draft = new DraftSession(Guid.NewGuid(), 4);
        var poolPlayers = new List<DraftPlayer>();
        for (int i = 0; i < 22; i++)
        {
            var p = new DraftPlayer(Guid.NewGuid(), $"Player_{i}");
            poolPlayers.Add(p);
            draft.AddPlayerToPool(p.Id, p.Name);
        }

        var teams = draft.Teams.ToList();
        for (int i = 0; i < 4; i++)
        {
            draft.SelectCaptain(poolPlayers[i].Id, draft.DraftVersion, teams[i].Id);
        }
        draft.AdvanceToCaptainOrderSelection(draft.DraftVersion);
        draft.FinalizeCaptainOrder(PickOrderType.Manual, teams.Select(t => t.Id).ToList(), draft.DraftVersion);

        for (int i = 4; i < 20; i++)
        {
            draft.PickStarterPlayer(poolPlayers[i].Id, draft.DraftVersion);
        }

        var reservePlayer = poolPlayers[20];
        draft.AllocateReserve(teams[0].Id, reservePlayer.Id, draft.DraftVersion);
        await draftRepo.AddAsync(draft);

        var cmd = new RemoveReserveCommand(draft.Id, teams[0].Id, reservePlayer.Id, draft.DraftVersion);
        var snapshot = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(1, notifier.NotificationCount);
        var updatedTeam0 = snapshot.Teams.First(t => t.Id == teams[0].Id);
        Assert.Empty(updatedTeam0.Reserves);
    }
}
