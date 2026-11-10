using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Matches.RecordWalkover;

public class RecordWalkoverHandler : IRequestHandler<RecordWalkoverCommand>
{
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly ITournamentRepository? _tournamentRepository;
    private readonly IBracketProgressionService? _progressionService;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public RecordWalkoverHandler(
        IMatchSeriesRepository matchRepository,
        ITournamentRepository? tournamentRepository = null,
        IBracketProgressionService? progressionService = null,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _matchRepository = matchRepository;
        _tournamentRepository = tournamentRepository;
        _progressionService = progressionService;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public RecordWalkoverHandler(
        IMatchSeriesRepository matchRepository,
        IBracketProgressionService progressionService)
        : this(matchRepository, null, progressionService, null)
    {
    }

    public async Task Handle(RecordWalkoverCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureResultSubmissionAllowed();

        var series = await _matchRepository.GetByIdAsync(request.MatchSeriesId, cancellationToken)
            ?? throw new DomainException($"Série '{request.MatchSeriesId}' não encontrada.");

        var tournament = _tournamentRepository != null ? await _tournamentRepository.GetByIdAsync(series.TournamentId, cancellationToken) : null;
        if (series.Status == MatchSeriesStatus.Pending || !series.TeamAId.HasValue || !series.TeamBId.HasValue)
        {
            throw new DomainException("Não é possível registrar resultado em uma partida cujas equipes ainda não foram definidas pelos confrontos anteriores.");
        }

        if (tournament != null)
        {
            if (tournament.IsResultSubmissionLocked)
            {
                throw new DomainException($"O envio e alteração de resultados para este campeonato está bloqueado pela administração. Motivo: {tournament.ResultLockReason ?? "Não informado"}.");
            }

            if (tournament.Status == TournamentStatus.Canceled)
            {
                throw new DomainException("Não é possível registrar resultados em um campeonato cancelado.");
            }

            if (tournament.Status == TournamentStatus.RegistrationOpen)
            {
                throw new DomainException("Não é possível registrar resultados em um campeonato que ainda não foi iniciado.");
            }
        }

        series.RecordWalkover(request.WinnerTeamId);

        await _matchRepository.UpdateAsync(series, cancellationToken);

        // Se a série foi concluída, avança o vencedor/perdedor para o próximo slot do mata-mata
        if (_progressionService != null)
        {
            await _progressionService.ProgressBracketAsync(series, cancellationToken);
        }
    }
}
