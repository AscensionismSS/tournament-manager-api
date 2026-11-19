using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Draft.AddPlayerToPool;

public class AddPlayerToPoolHandler : IRequestHandler<AddPlayerToPoolCommand, DraftStateSnapshotDto>
{
    private readonly IDraftSessionRepository _draftRepository;
    private readonly ITournamentRealtimeNotifier _notifier;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;
    private readonly ITournamentRepository? _tournamentRepository;

    public AddPlayerToPoolHandler(
        IDraftSessionRepository draftRepository,
        ITournamentRealtimeNotifier notifier,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null,
        ITournamentRepository? tournamentRepository = null)
    {
        _draftRepository = draftRepository;
        _notifier = notifier;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
        _tournamentRepository = tournamentRepository;
    }

    public async Task<DraftStateSnapshotDto> Handle(AddPlayerToPoolCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureTournamentRegistrationAllowed();

        var session = await _draftRepository.GetByIdAsync(request.DraftSessionId, cancellationToken)
            ?? throw new DomainException($"Sessão de Draft '{request.DraftSessionId}' não encontrada.");

        if (!request.IsAdmin)
        {
            if (session.State != DraftState.SelectingCaptains)
            {
                throw new DomainException("As inscrições de jogadores já foram encerradas para este draft.");
            }

            if (_tournamentRepository is not null)
            {
                var tournament = await _tournamentRepository.GetByIdAsync(session.TournamentId, cancellationToken);
                if (tournament is not null && tournament.Status != TournamentStatus.RegistrationOpen)
                {
                    throw new DomainException("As inscrições não estão abertas para este campeonato no momento.");
                }
            }
        }

        session.AddPlayerToPool(
            request.PlayerId,
            request.Name,
            request.ExternalUserId,
            request.Roles,
            request.PreferredRole,
            request.RankTier,
            request.AvatarUrl
        );
        await _draftRepository.UpdateAsync(session, cancellationToken);

        var snapshot = DraftSnapshotMapper.ToSnapshotDto(session);
        await _notifier.NotifyDraftStateSnapshotAsync(session.TournamentId, snapshot, cancellationToken);

        return snapshot;
    }
}
