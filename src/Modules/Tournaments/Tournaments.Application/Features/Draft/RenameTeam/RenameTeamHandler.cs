using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Draft;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Draft.RenameTeam;

public class RenameTeamHandler : IRequestHandler<RenameTeamCommand, DraftStateSnapshotDto>
{
    private readonly IDraftSessionRepository _draftRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITournamentRealtimeNotifier _notifier;
    private readonly IMatchSeriesRepository? _matchRepository;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public RenameTeamHandler(
        IDraftSessionRepository draftRepository,
        ITournamentRepository tournamentRepository,
        ITournamentRealtimeNotifier notifier,
        IMatchSeriesRepository? matchRepository = null,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _draftRepository = draftRepository;
        _tournamentRepository = tournamentRepository;
        _notifier = notifier;
        _matchRepository = matchRepository;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task<DraftStateSnapshotDto> Handle(RenameTeamCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureDraftPicksAllowed();

        var session = await _draftRepository.GetByIdAsync(request.DraftSessionId, cancellationToken)
            ?? throw new DomainException($"Sessão de Draft '{request.DraftSessionId}' não encontrada.");

        // Valida se o campeonato já avançou para os Playoffs ou foi finalizado
        var tournament = await _tournamentRepository.GetByIdAsync(session.TournamentId, cancellationToken);
        if (tournament != null)
        {
            if (tournament.Status == TournamentStatus.Playoffs || tournament.Status == TournamentStatus.Completed)
            {
                throw new DomainException("Não é permitido alterar o nome da equipe após o avanço para a fase de Playoffs ou conclusão do torneio.");
            }
        }

        // Regras de negócio protegidas pelo Domínio:
        // 1. Apenas administradores do torneio têm permissão para alterar o nome da equipe
        // 2. Validação de concorrência por ExpectedVersion
        session.RenameTeam(request.TeamId, request.NewName, request.ExpectedVersion, request.ActingUserId, request.IsAdmin);
        await _draftRepository.UpdateAsync(session, cancellationToken);

        // Se houver torneio vinculado e a equipe já tiver sido sincronizada, mantém consistência no Torneio
        var updatedTeam = session.Teams.FirstOrDefault(t => t.Id == request.TeamId);
        if (tournament != null && updatedTeam != null && tournament.Teams.Any(t => t.Id == request.TeamId))
        {
            tournament.RenameTeam(request.TeamId, updatedTeam.Name);
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
        }

        // Sincroniza partidas já geradas na fase de grupos (se houver)
        if (_matchRepository != null && updatedTeam != null)
        {
            var matches = await _matchRepository.GetByTournamentIdAsync(session.TournamentId, cancellationToken);
            foreach (var match in matches)
            {
                if (match.TeamAId == request.TeamId || match.TeamBId == request.TeamId)
                {
                    match.UpdateTeamName(request.TeamId, updatedTeam.Name);
                    await _matchRepository.UpdateAsync(match, cancellationToken);
                }
            }
        }

        var snapshot = DraftSnapshotMapper.ToSnapshotDto(session);
        await _notifier.NotifyDraftStateSnapshotAsync(session.TournamentId, snapshot, cancellationToken);

        return snapshot;
    }
}
