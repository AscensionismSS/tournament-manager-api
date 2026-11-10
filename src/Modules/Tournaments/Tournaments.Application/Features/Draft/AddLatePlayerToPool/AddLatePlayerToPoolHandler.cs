using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Draft.AddLatePlayerToPool;

public class AddLatePlayerToPoolHandler : IRequestHandler<AddLatePlayerToPoolCommand, DraftStateSnapshotDto>
{
    private readonly IDraftSessionRepository _draftRepository;
    private readonly ITournamentRealtimeNotifier _notifier;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public AddLatePlayerToPoolHandler(
        IDraftSessionRepository draftRepository,
        ITournamentRealtimeNotifier notifier,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _draftRepository = draftRepository;
        _notifier = notifier;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task<DraftStateSnapshotDto> Handle(AddLatePlayerToPoolCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureTournamentRegistrationAllowed();

        var session = await _draftRepository.GetByIdAsync(request.DraftSessionId, cancellationToken)
            ?? throw new DomainException($"Sessão de Draft '{request.DraftSessionId}' não encontrada.");

        session.AddLatePlayerToPool(
            request.PlayerId,
            request.Name,
            request.ExternalUserId,
            request.Roles,
            request.PreferredRole,
            request.RankTier,
            request.AvatarUrl,
            request.ExpectedVersion
        );

        await _draftRepository.UpdateAsync(session, cancellationToken);

        var snapshot = DraftSnapshotMapper.ToSnapshotDto(session);
        await _notifier.NotifyDraftStateSnapshotAsync(session.TournamentId, snapshot, cancellationToken);

        return snapshot;
    }
}
