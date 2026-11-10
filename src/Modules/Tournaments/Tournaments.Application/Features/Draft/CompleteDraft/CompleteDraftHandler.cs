using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Draft.CompleteDraft;

public class CompleteDraftHandler : IRequestHandler<CompleteDraftCommand, DraftStateSnapshotDto>
{
    private readonly IDraftSessionRepository _draftRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITournamentRealtimeNotifier _notifier;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public CompleteDraftHandler(
        IDraftSessionRepository draftRepository,
        ITournamentRepository tournamentRepository,
        ITournamentRealtimeNotifier notifier,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _draftRepository = draftRepository;
        _tournamentRepository = tournamentRepository;
        _notifier = notifier;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task<DraftStateSnapshotDto> Handle(CompleteDraftCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureDraftPicksAllowed();

        var session = await _draftRepository.GetByIdAsync(request.DraftSessionId, cancellationToken)
            ?? throw new DomainException($"Sessão de Draft '{request.DraftSessionId}' não encontrada.");

        session.CompleteDraft(request.ExpectedVersion);
        await _draftRepository.UpdateAsync(session, cancellationToken);

        // Se houver torneio vinculado, exporta os times para o torneio
        var tournament = await _tournamentRepository.GetByIdAsync(session.TournamentId, cancellationToken);
        if (tournament != null)
        {
            foreach (var team in session.Teams)
            {
                if (!tournament.Teams.Any(t => t.Id == team.Id))
                {
                    tournament.AddTeam(team.Id, team.Name);
                }
            }
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
        }

        var snapshot = DraftSnapshotMapper.ToSnapshotDto(session);
        await _notifier.NotifyDraftCompletedAsync(
            session.TournamentId,
            new DraftCompletedEventDto(session.Id, session.TournamentId, session.DraftVersion),
            cancellationToken);

        return snapshot;
    }
}
