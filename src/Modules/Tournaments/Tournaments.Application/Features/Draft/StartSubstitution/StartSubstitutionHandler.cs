using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Draft.StartSubstitution;

public class StartSubstitutionHandler : IRequestHandler<StartSubstitutionCommand, DraftStateSnapshotDto>
{
    private readonly IDraftSessionRepository _draftRepository;
    private readonly ITournamentRealtimeNotifier _notifier;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public StartSubstitutionHandler(
        IDraftSessionRepository draftRepository,
        ITournamentRealtimeNotifier notifier,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _draftRepository = draftRepository;
        _notifier = notifier;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task<DraftStateSnapshotDto> Handle(StartSubstitutionCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureDraftPicksAllowed();

        var session = await _draftRepository.GetByIdAsync(request.DraftSessionId, cancellationToken)
            ?? throw new DomainException($"Sessão de Draft '{request.DraftSessionId}' não encontrada.");

        session.StartSubstitution(request.PlayerId, request.TeamId, request.ExpectedVersion);
        await _draftRepository.UpdateAsync(session, cancellationToken);

        var snapshot = DraftSnapshotMapper.ToSnapshotDto(session);
        await _notifier.NotifySubstitutionStartedAsync(
            session.TournamentId,
            new SubstitutionStartedEventDto(session.Id, session.TournamentId, request.TeamId, request.PlayerId, session.DraftVersion),
            cancellationToken);

        return snapshot;
    }
}
