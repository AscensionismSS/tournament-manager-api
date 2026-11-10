using BuildingBlocks.Domain;
using Tournaments.Application.Features.Draft.AddLatePlayerToPool;
using Tournaments.Application.Features.Draft.AddPlayerToPool;
using Tournaments.Application.Features.Draft.AdvanceToCaptainOrderSelection;
using Tournaments.Application.Features.Draft.AdvanceToSubstitutes;
using Tournaments.Application.Features.Draft.AllocateReserve;
using Tournaments.Application.Features.Draft.CancelSubstitution;
using Tournaments.Application.Features.Draft.CompleteDraft;
using Tournaments.Application.Features.Draft.ConfirmSubstitution;
using Tournaments.Application.Features.Draft.FinalizeCaptainOrder;
using Tournaments.Application.Features.Draft.PickStarterPlayer;
using Tournaments.Application.Features.Draft.RemoveCaptain;
using Tournaments.Application.Features.Draft.RemoveReserve;
using Tournaments.Application.Features.Draft.RenameTeam;
using Tournaments.Application.Features.Draft.SelectCaptain;
using Tournaments.Application.Features.Draft.StartSubstitution;
using Tournaments.Application.Features.Matches.RecordManualResult;
using Tournaments.Application.Features.Matches.RecordReplayResult;
using Tournaments.Application.Features.Matches.RecordWalkover;
using Tournaments.Application.Features.Matches.RemoveLastGame;
using Tournaments.Application.Features.Matches.RollbackMatchSeries;
using Tournaments.Application.Features.Matches.UpdateGameResult;
using Tournaments.Application.Features.Matches.UpdateGameWalkover;
using Tournaments.Application.Features.Matches.UploadMatchReplay;
using Tournaments.Application.Features.Security.GetSystemSecuritySettings;
using Tournaments.Application.Features.Security.UpdateSystemSecuritySettings;
using Tournaments.Application.Features.Tournaments.CreateTournament;
using Tournaments.Application.Features.Tournaments.OpenRegistrations;
using Tournaments.Application.Features.Tournaments.StartTournament;
using Tournaments.Contracts.Matches;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;
using Tournaments.Domain.ValueObjects;
using Tournaments.Infrastructure.Repositories;
using Xunit;

namespace Tournaments.UnitTests;

public class GlobalSecuritySettingsTests
{
    [Fact]
    public void SystemSecuritySettings_CreateDefault_ShouldInitializeWithAllLocksOpen()
    {
        var settings = SystemSecuritySettings.CreateDefault();

        Assert.Equal(SystemSecuritySettings.DefaultSettingsId, settings.Id);
        Assert.False(settings.LockResultSubmission);
        Assert.False(settings.LockTournamentRegistration);
        Assert.False(settings.LockTournamentCreation);
        Assert.False(settings.LockDraftPicks);
        Assert.False(settings.MaintenanceMode);
        Assert.Null(settings.Message);
        Assert.False(settings.IsResultSubmissionLocked);
        Assert.False(settings.IsTournamentRegistrationLocked);
        Assert.False(settings.IsTournamentCreationLocked);
        Assert.False(settings.IsDraftPicksLocked);

        // Guard methods should not throw
        settings.EnsureResultSubmissionAllowed();
        settings.EnsureTournamentRegistrationAllowed();
        settings.EnsureTournamentCreationAllowed();
        settings.EnsureDraftPicksAllowed();
    }

    [Fact]
    public void SystemSecuritySettings_WhenIndividualLocksActive_ShouldThrowDomainExceptionWithCustomMessage()
    {
        var customMessage = "Servidores em atualização técnica";
        var settings = new SystemSecuritySettings(
            lockResultSubmission: true,
            lockTournamentRegistration: true,
            lockTournamentCreation: true,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: customMessage,
            updatedBy: "SuperAdmin"
        );

        Assert.True(settings.IsResultSubmissionLocked);
        Assert.True(settings.IsTournamentRegistrationLocked);
        Assert.True(settings.IsTournamentCreationLocked);
        Assert.True(settings.IsDraftPicksLocked);

        var exResult = Assert.Throws<DomainException>(() => settings.EnsureResultSubmissionAllowed());
        Assert.Contains(customMessage, exResult.Message);

        var exReg = Assert.Throws<DomainException>(() => settings.EnsureTournamentRegistrationAllowed());
        Assert.Contains(customMessage, exReg.Message);

        var exCreate = Assert.Throws<DomainException>(() => settings.EnsureTournamentCreationAllowed());
        Assert.Contains(customMessage, exCreate.Message);

        var exDraft = Assert.Throws<DomainException>(() => settings.EnsureDraftPicksAllowed());
        Assert.Contains(customMessage, exDraft.Message);
    }

    [Fact]
    public void SystemSecuritySettings_WhenMaintenanceModeActive_ShouldLockAllOperationsEvenIfIndividualFlagsFalse()
    {
        var settings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: true,
            message: "Plataforma indisponível temporariamente",
            updatedBy: "SuperAdmin"
        );

        Assert.True(settings.IsResultSubmissionLocked);
        Assert.True(settings.IsTournamentRegistrationLocked);
        Assert.True(settings.IsTournamentCreationLocked);
        Assert.True(settings.IsDraftPicksLocked);

        Assert.Throws<DomainException>(() => settings.EnsureResultSubmissionAllowed());
        Assert.Throws<DomainException>(() => settings.EnsureTournamentRegistrationAllowed());
        Assert.Throws<DomainException>(() => settings.EnsureTournamentCreationAllowed());
        Assert.Throws<DomainException>(() => settings.EnsureDraftPicksAllowed());
    }

    [Fact]
    public async Task CreateTournamentHandler_WhenTournamentCreationLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: true,
            lockDraftPicks: false,
            maintenanceMode: false,
            message: "Criação de novos torneios congelada",
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var tournamentRepo = new InMemoryTournamentRepository();
        var draftRepo = new InMemoryDraftSessionRepository();

        var handler = new CreateTournamentHandler(tournamentRepo, draftRepo, securityRepo);
        var command = new CreateTournamentCommand(
            Name: "Copa Bloqueada",
            Type: TournamentType.DirectPlayoffs,
            PlayoffFormat: PlayoffFormat.SingleElimination,
            TeamCount: 4,
            GroupCount: 1,
            GroupStageFormat: null,
            PlayoffSeriesFormat: SeriesFormat.Bo3,
            GrandFinalSeriesFormat: SeriesFormat.Bo5,
            EnableDraftMode: false);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("criação de novos torneios está temporariamente bloqueada", ex.Message);
        Assert.Contains("Criação de novos torneios congelada", ex.Message);
    }

    [Fact]
    public async Task AddPlayerToPoolHandler_WhenRegistrationLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: true,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: false,
            message: "Inscrições encerradas pelo administrador",
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new AddPlayerToPoolHandler(draftRepo, notifier, securityRepo);
        var command = new AddPlayerToPoolCommand(Guid.NewGuid(), Guid.NewGuid(), "Player1", "user-1", ["Mid"], "Mid", 10, null);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("inscrição de jogadores em torneios está temporariamente bloqueada", ex.Message);
        Assert.Contains("Inscrições encerradas pelo administrador", ex.Message);
    }

    [Fact]
    public async Task AddLatePlayerToPoolHandler_WhenRegistrationLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: true,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new AddLatePlayerToPoolHandler(draftRepo, notifier, securityRepo);
        var command = new AddLatePlayerToPoolCommand(Guid.NewGuid(), Guid.NewGuid(), "LatePlayer", "user-2", ["Support"], "Support", 20, null, 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("inscrição de jogadores em torneios está temporariamente bloqueada", ex.Message);
    }

    [Fact]
    public async Task OpenRegistrationsHandler_WhenRegistrationLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: true,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var tournamentRepo = new InMemoryTournamentRepository();

        var handler = new OpenRegistrationsHandler(tournamentRepo, null, securityRepo);
        var command = new OpenRegistrationsCommand(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("inscrição de jogadores em torneios está temporariamente bloqueada", ex.Message);
    }

    [Fact]
    public async Task RecordManualResultHandler_WhenResultSubmissionLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: true,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: false,
            message: "Envio de resultados bloqueado temporariamente",
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var matchRepo = new InMemoryMatchSeriesRepository();

        var handler = new RecordManualResultHandler(matchRepo, null, null, securityRepo);
        var command = new RecordManualResultCommand(
            MatchSeriesId: Guid.NewGuid(),
            WinnerTeamId: Guid.NewGuid(),
            DurationSeconds: 2400,
            TeamAKills: 30,
            TeamBKills: 20);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("envio e alteração de resultados de partidas está temporariamente bloqueado", ex.Message);
        Assert.Contains("Envio de resultados bloqueado temporariamente", ex.Message);
    }

    [Fact]
    public async Task RecordWalkoverHandler_WhenResultSubmissionLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: true,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var matchRepo = new InMemoryMatchSeriesRepository();

        var handler = new RecordWalkoverHandler(matchRepo, null, null, securityRepo);
        var command = new RecordWalkoverCommand(Guid.NewGuid(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("envio e alteração de resultados de partidas está temporariamente bloqueado", ex.Message);
    }

    [Fact]
    public async Task PickStarterPlayerHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: "Draft paralisado por decisão da coordenação",
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new PickStarterPlayerHandler(draftRepo, notifier, securityRepo);
        var command = new PickStarterPlayerCommand(Guid.NewGuid(), Guid.NewGuid(), 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
        Assert.Contains("Draft paralisado por decisão da coordenação", ex.Message);
    }

    [Fact]
    public async Task StartSubstitutionHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new StartSubstitutionHandler(draftRepo, notifier, securityRepo);
        var command = new StartSubstitutionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
    }

    [Fact]
    public async Task AllocateReserveHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new AllocateReserveHandler(draftRepo, notifier, securityRepo);
        var command = new AllocateReserveCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
    }

    [Fact]
    public void ReplayValidationService_WhenSecuritySettingsHasLockedResults_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: true,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: false,
            message: "Replays desabilitados para verificação",
            updatedBy: "SuperAdmin"
        );

        var service = new ReplayValidationService();
        var series = new MatchSeries(Guid.NewGuid(), "M1", SeriesFormat.Bo3, 1);
        series.AssignTeams(Guid.NewGuid(), "Time 1", Guid.NewGuid(), "Time 2");

        var ex = Assert.Throws<DomainException>(() => service.ValidateSeriesAndTournamentState(series, null, 1, lockedSettings));
        Assert.Contains("envio e alteração de resultados de partidas está temporariamente bloqueado", ex.Message);
        Assert.Contains("Replays desabilitados para verificação", ex.Message);
    }

    [Fact]
    public async Task GetAndUpdateSystemSecuritySettingsHandlers_ShouldWorkCorrectly()
    {
        var repo = new DefaultSystemSecuritySettingsRepository();
        var notifier = new TestRealtimeNotifier();

        var getHandler = new GetSystemSecuritySettingsHandler(repo);
        var initialDto = await getHandler.Handle(new GetSystemSecuritySettingsQuery(), CancellationToken.None);

        Assert.False(initialDto.LockResultSubmission);
        Assert.False(initialDto.MaintenanceMode);

        var updateHandler = new UpdateSystemSecuritySettingsHandler(repo, notifier);
        var updatedDto = await updateHandler.Handle(new UpdateSystemSecuritySettingsCommand(
            LockResultSubmission: true,
            LockTournamentRegistration: true,
            LockTournamentCreation: false,
            LockDraftPicks: false,
            MaintenanceMode: false,
            Message: "Travamento de emergência",
            UpdatedBy: "SuperAdmin"
        ), CancellationToken.None);

        Assert.True(updatedDto.LockResultSubmission);
        Assert.True(updatedDto.LockTournamentRegistration);
        Assert.False(updatedDto.LockTournamentCreation);
        Assert.False(updatedDto.LockDraftPicks);
        Assert.False(updatedDto.MaintenanceMode);
        Assert.Equal("Travamento de emergência", updatedDto.Message);
        Assert.Equal("SuperAdmin", updatedDto.UpdatedBy);

        var refetchedDto = await getHandler.Handle(new GetSystemSecuritySettingsQuery(), CancellationToken.None);
        Assert.True(refetchedDto.LockResultSubmission);
        Assert.True(refetchedDto.LockTournamentRegistration);
        Assert.Equal("Travamento de emergência", refetchedDto.Message);

        // Quando todas as travas são desligadas sem passar nova mensagem, a mensagem antiga é limpa automaticamente
        var unlockedDto = await updateHandler.Handle(new UpdateSystemSecuritySettingsCommand(
            LockResultSubmission: false,
            LockTournamentRegistration: false,
            LockTournamentCreation: false,
            LockDraftPicks: false,
            MaintenanceMode: false,
            Message: null,
            UpdatedBy: "SuperAdmin"
        ), CancellationToken.None);

        Assert.False(unlockedDto.LockResultSubmission);
        Assert.False(unlockedDto.MaintenanceMode);
        Assert.Null(unlockedDto.Message);
    }

    [Fact]
    public async Task RemoveCaptainHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: "Draft congelado",
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new RemoveCaptainHandler(draftRepo, notifier, securityRepo);
        var command = new RemoveCaptainCommand(Guid.NewGuid(), Guid.NewGuid(), 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
        Assert.Contains("Draft congelado", ex.Message);
    }

    [Fact]
    public async Task AdvanceToCaptainOrderSelectionHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new AdvanceToCaptainOrderSelectionHandler(draftRepo, notifier, securityRepo);
        var command = new AdvanceToCaptainOrderSelectionCommand(Guid.NewGuid(), 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
    }

    [Fact]
    public async Task FinalizeCaptainOrderHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new FinalizeCaptainOrderHandler(draftRepo, notifier, securityRepo);
        var command = new FinalizeCaptainOrderCommand(Guid.NewGuid(), PickOrderType.Manual, new List<Guid>(), 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
    }

    [Fact]
    public async Task AdvanceToSubstitutesHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new AdvanceToSubstitutesHandler(draftRepo, notifier, securityRepo);
        var command = new AdvanceToSubstitutesCommand(Guid.NewGuid(), 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
    }

    [Fact]
    public async Task CompleteDraftHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new CompleteDraftHandler(draftRepo, tournamentRepo, notifier, securityRepo);
        var command = new CompleteDraftCommand(Guid.NewGuid(), 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
    }

    [Fact]
    public async Task RenameTeamHandler_WhenDraftPicksLocked_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: true,
            maintenanceMode: false,
            message: null,
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var draftRepo = new InMemoryDraftSessionRepository();
        var tournamentRepo = new InMemoryTournamentRepository();
        var notifier = new TestRealtimeNotifier();

        var handler = new RenameTeamHandler(draftRepo, tournamentRepo, notifier, null, securityRepo);
        var command = new RenameTeamCommand(Guid.NewGuid(), Guid.NewGuid(), "Novo Nome", 1);

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("seleção de jogadores e operações de draft estão temporariamente bloqueadas", ex.Message);
    }

    [Fact]
    public async Task StartTournamentHandler_WhenMaintenanceModeActive_ShouldThrowDomainException()
    {
        var lockedSettings = new SystemSecuritySettings(
            lockResultSubmission: false,
            lockTournamentRegistration: false,
            lockTournamentCreation: false,
            lockDraftPicks: false,
            maintenanceMode: true,
            message: "Manutenção geral preventiva",
            updatedBy: "SuperAdmin"
        );
        var securityRepo = new DefaultSystemSecuritySettingsRepository(lockedSettings);
        var tournamentRepo = new InMemoryTournamentRepository();
        var matchRepo = new InMemoryMatchSeriesRepository();

        var handler = new StartTournamentHandler(tournamentRepo, matchRepo, null, securityRepo);
        var command = new StartTournamentCommand(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Não é possível iniciar o torneio durante o modo de manutenção do sistema", ex.Message);
        Assert.Contains("Manutenção geral preventiva", ex.Message);
    }
}
