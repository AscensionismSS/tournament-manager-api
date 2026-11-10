using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Security;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Security.UpdateSystemSecuritySettings;

public class UpdateSystemSecuritySettingsHandler : IRequestHandler<UpdateSystemSecuritySettingsCommand, SystemSecuritySettingsDto>
{
    private readonly ISystemSecuritySettingsRepository _repository;
    private readonly ITournamentRealtimeNotifier _notifier;

    public UpdateSystemSecuritySettingsHandler(
        ISystemSecuritySettingsRepository repository,
        ITournamentRealtimeNotifier notifier)
    {
        _repository = repository;
        _notifier = notifier;
    }

    public async Task<SystemSecuritySettingsDto> Handle(UpdateSystemSecuritySettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _repository.GetAsync(cancellationToken);

        var lockResultSubmission = request.LockResultSubmission ?? settings.LockResultSubmission;
        var lockTournamentRegistration = request.LockTournamentRegistration ?? settings.LockTournamentRegistration;
        var lockTournamentCreation = request.LockTournamentCreation ?? settings.LockTournamentCreation;
        var lockDraftPicks = request.LockDraftPicks ?? settings.LockDraftPicks;
        var maintenanceMode = request.MaintenanceMode ?? settings.MaintenanceMode;

        var isAnyLocked = lockResultSubmission || lockTournamentRegistration || lockTournamentCreation || lockDraftPicks || maintenanceMode;
        var message = request.Message != null
            ? (string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim())
            : (isAnyLocked ? settings.Message : null);

        settings.Update(
            lockResultSubmission,
            lockTournamentRegistration,
            lockTournamentCreation,
            lockDraftPicks,
            maintenanceMode,
            message,
            request.UpdatedBy,
            DateTime.UtcNow
        );

        await _repository.UpdateAsync(settings, cancellationToken);

        var dto = settings.ToDto();
        await _notifier.NotifySecuritySettingsChangedAsync(dto, cancellationToken);

        return dto;
    }
}
