using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Matches.RecordManualResult;

public class RecordManualResultHandler : IRequestHandler<RecordManualResultCommand>
{
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly ITournamentRepository? _tournamentRepository;
    private readonly IBracketProgressionService? _progressionService;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public RecordManualResultHandler(
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

    public RecordManualResultHandler(
        IMatchSeriesRepository matchRepository,
        IBracketProgressionService progressionService)
        : this(matchRepository, null, progressionService, null)
    {
    }

    public async Task Handle(RecordManualResultCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureResultSubmissionAllowed();

        var series = await _matchRepository.GetByIdAsync(request.MatchSeriesId, cancellationToken)
            ?? throw new DomainException($"Série '{request.MatchSeriesId}' não encontrada.");

        var tournament = _tournamentRepository != null ? await _tournamentRepository.GetByIdAsync(series.TournamentId, cancellationToken) : null;
        if (series.Status == MatchSeriesStatus.Pending || !series.TeamAId.HasValue || !series.TeamBId.HasValue)
        {
            throw new DomainException("Ambas as equipes precisam estar definidas na série antes de registrar um resultado.");
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

        var existingGame = request.GameNumber.HasValue
            ? series.Games.FirstOrDefault(g => g.GameNumber == request.GameNumber.Value)
            : null;

        if (existingGame == null)
        {
            if (request.GameNumber.HasValue)
            {
                if (request.GameNumber.Value <= 0)
                {
                    throw new DomainException("O número do jogo deve ser maior que zero.");
                }

                if (request.GameNumber.Value > series.Games.Count + 1)
                {
                    throw new DomainException($"Não é possível registrar o Jogo #{request.GameNumber.Value} antes de registrar o Jogo #{request.GameNumber.Value - 1}.");
                }

                if (request.GameNumber.Value != series.Games.Count + 1)
                {
                    throw new DomainException($"Jogo número {request.GameNumber.Value} não encontrado na série.");
                }
            }

            if (series.Status == MatchSeriesStatus.Completed)
            {
                throw new DomainException($"A série #{series.Id} já foi finalizada ({series.ScoreTeamA} x {series.ScoreTeamB}). Não é possível adicionar novos jogos.");
            }
        }

        if (request.WinnerTeamId != series.TeamAId && request.WinnerTeamId != series.TeamBId)
        {
            throw new DomainException("O time vencedor do mapa deve ser uma das equipes da série.");
        }

        if (request.FirstPickTeamId.HasValue && request.FirstPickTeamId.Value != Guid.Empty &&
            request.FirstPickTeamId.Value != series.TeamAId && request.FirstPickTeamId.Value != series.TeamBId)
        {
            throw new DomainException("O time do First Pick deve ser uma das equipes da série.");
        }

        var wasCompleted = series.Status == MatchSeriesStatus.Completed;

        if (existingGame != null)
        {
            if (wasCompleted && _progressionService != null)
            {
                await _progressionService.UndoProgressionOnlyAsync(series, cancellationToken);
            }

            series.UpdateGame(
                request.GameNumber!.Value,
                request.WinnerTeamId,
                request.DurationSeconds,
                request.TeamAKills,
                request.TeamBKills,
                request.FirstPickTeamId,
                request.TeamABanHeroIds,
                request.TeamBBanHeroIds,
                request.DotaMatchId);
        }
        else
        {
            series.RecordGameManual(
                request.WinnerTeamId,
                request.DurationSeconds,
                request.TeamAKills,
                request.TeamBKills,
                request.FirstPickTeamId,
                request.TeamABanHeroIds,
                request.TeamBBanHeroIds,
                request.DotaMatchId,
                request.GameNumber);
        }

        await _matchRepository.UpdateAsync(series, cancellationToken);

        // Se a série foi concluída, avança o vencedor/perdedor para o próximo slot do mata-mata
        if (_progressionService != null)
        {
            await _progressionService.ProgressBracketAsync(series, cancellationToken);
        }
    }
}
