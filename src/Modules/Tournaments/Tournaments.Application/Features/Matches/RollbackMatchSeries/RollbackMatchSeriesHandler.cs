using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Matches.RollbackMatchSeries;

public class RollbackMatchSeriesHandler : IRequestHandler<RollbackMatchSeriesCommand>
{
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly IBracketProgressionService _progressionService;
    private readonly ITournamentRepository? _tournamentRepository;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public RollbackMatchSeriesHandler(
        IMatchSeriesRepository matchRepository,
        IBracketProgressionService progressionService)
        : this(matchRepository, progressionService, null, null)
    {
    }

    public RollbackMatchSeriesHandler(
        IMatchSeriesRepository matchRepository,
        IBracketProgressionService progressionService,
        ITournamentRepository? tournamentRepository,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _matchRepository = matchRepository;
        _progressionService = progressionService;
        _tournamentRepository = tournamentRepository;
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task Handle(RollbackMatchSeriesCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureResultSubmissionAllowed();

        var series = await _matchRepository.GetByIdAsync(request.MatchSeriesId, cancellationToken)
            ?? throw new DomainException($"Série '{request.MatchSeriesId}' não encontrada.");

        var tournament = _tournamentRepository != null ? await _tournamentRepository.GetByIdAsync(series.TournamentId, cancellationToken) : null;
        if (tournament != null && tournament.IsResultSubmissionLocked)
        {
            throw new DomainException($"O envio e alteração de resultados para este campeonato está bloqueado pela administração. Motivo: {tournament.ResultLockReason ?? "Não informado"}.");
        }

        await _progressionService.RollbackBracketProgressionAsync(series, cancellationToken);
    }
}
