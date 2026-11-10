using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Matches.UpdateGameWalkover;

public class UpdateGameWalkoverHandler : IRequestHandler<UpdateGameWalkoverCommand>
{
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly ITournamentRepository? _tournamentRepository;
    private readonly IBracketProgressionService? _progressionService;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public UpdateGameWalkoverHandler(
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

    public UpdateGameWalkoverHandler(
        IMatchSeriesRepository matchRepository,
        IBracketProgressionService progressionService)
        : this(matchRepository, null, progressionService, null)
    {
    }

    public async Task Handle(UpdateGameWalkoverCommand request, CancellationToken cancellationToken)
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

        var existingGame = series.Games.FirstOrDefault(g => g.GameNumber == request.GameNumber);
        if (existingGame == null)
        {
            if (request.GameNumber > series.Games.Count + 1)
            {
                throw new DomainException($"Não é possível registrar o Jogo #{request.GameNumber} antes de registrar o Jogo #{request.GameNumber - 1}.");
            }
            if (request.GameNumber != series.Games.Count + 1)
            {
                throw new DomainException($"Jogo número {request.GameNumber} não encontrado na série.");
            }
            if (series.Status == MatchSeriesStatus.Completed)
            {
                throw new DomainException($"A série #{series.Id} já foi finalizada ({series.ScoreTeamA} x {series.ScoreTeamB}). Não é possível adicionar novos jogos.");
            }
        }

        var wasCompleted = series.Status == MatchSeriesStatus.Completed;

        // Se a série estava concluída e estamos editando um jogo existente, desfaz a progressão anterior com verificação de segurança downstream
        if (existingGame != null && wasCompleted && _progressionService != null)
        {
            await _progressionService.UndoProgressionOnlyAsync(series, cancellationToken);
        }

        series.UpdateGameWalkover(request.GameNumber, request.WinnerTeamId);

        await _matchRepository.UpdateAsync(series, cancellationToken);

        // Atualiza a progressão do torneio ou registra evento de mapa em andamento
        if (_progressionService != null)
        {
            await _progressionService.ProgressBracketAsync(series, cancellationToken);
        }
    }
}
