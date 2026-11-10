using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Draft.AdvanceToSubstitutes;

public class AdvanceToSubstitutesHandler : IRequestHandler<AdvanceToSubstitutesCommand, DraftStateSnapshotDto>
{
    private readonly IDraftSessionRepository _draftRepository;
    private readonly ITournamentRealtimeNotifier _notifier;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public AdvanceToSubstitutesHandler(
        IDraftSessionRepository draftRepository,
        ITournamentRealtimeNotifier notifier,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _draftRepository = draftRepository;
        _notifier = notifier;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task<DraftStateSnapshotDto> Handle(AdvanceToSubstitutesCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureDraftPicksAllowed();

        var session = await _draftRepository.GetByIdAsync(request.DraftSessionId, cancellationToken)
            ?? throw new DomainException($"Sessão de Draft '{request.DraftSessionId}' não encontrada.");

        session.AdvanceToSubstitutes(request.ExpectedVersion);
        await _draftRepository.UpdateAsync(session, cancellationToken);

        var snapshot = DraftSnapshotMapper.ToSnapshotDto(session);
        await _notifier.NotifyDraftStateSnapshotAsync(session.TournamentId, snapshot, cancellationToken);

        return snapshot;
    }
}

