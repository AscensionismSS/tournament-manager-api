using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Matches.RecordReplayResult;

public class RecordReplayResultHandler : IRequestHandler<RecordReplayResultCommand>
{
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly ITournamentRepository? _tournamentRepository;
    private readonly IBracketProgressionService? _progressionService;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public RecordReplayResultHandler(
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

    public RecordReplayResultHandler(
        IMatchSeriesRepository matchRepository,
        IBracketProgressionService progressionService)
        : this(matchRepository, null, progressionService, null)
    {
    }

    public async Task Handle(RecordReplayResultCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureResultSubmissionAllowed();

        var dto = request.IngestionDto;
        var series = await _matchRepository.GetByIdAsync(dto.MatchSeriesId, cancellationToken)
            ?? throw new DomainException($"Série '{dto.MatchSeriesId}' não encontrada.");

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

        var domainStats = dto.PlayerStats?.Select(p => new PlayerGameStats(
            p.PlayerId,
            p.TeamId,
            p.HeroId,
            p.Kills,
            p.Deaths,
            p.Assists,
            p.NetWorth,
            p.GoldPerMinute,
            p.ExperiencePerMinute,
            p.ItemIds,
            p.PlayerName,
            p.SteamName,
            p.HeroName,
            p.ItemNames,
            p.BackpackNames,
            p.NeutralItem,
            p.NeutralItems,
            p.JungleBuff,
            p.JungleBuffs,
            p.HasAghanimScepter,
            p.HasAghanimShard,
            p.WardsPlaced,
            p.WardsDestroyed,
            p.TowerDamage,
            p.HeroDamage,
            p.HeroHealing
        )).ToList();

        var wasCompleted = series.Status == MatchSeriesStatus.Completed;
        var existingGame = series.Games.FirstOrDefault(g => g.GameNumber == dto.GameNumber);

        if (existingGame == null)
        {
            if (dto.GameNumber > series.Games.Count + 1)
            {
                throw new DomainException($"Não é possível registrar o Jogo #{dto.GameNumber} antes de registrar o Jogo #{dto.GameNumber - 1}.");
            }
            if (dto.GameNumber != series.Games.Count + 1)
            {
                throw new DomainException($"Jogo número {dto.GameNumber} não encontrado na série.");
            }
            if (series.Status == MatchSeriesStatus.Completed)
            {
                throw new DomainException($"A série #{series.Id} já foi finalizada ({series.ScoreTeamA} x {series.ScoreTeamB}). Não é possível adicionar novos jogos.");
            }
        }

        if (existingGame != null)
        {
            if (wasCompleted && _progressionService != null)
            {
                await _progressionService.UndoProgressionOnlyAsync(series, cancellationToken);
            }

            series.UpdateGameFromReplay(
                dto.GameNumber,
                dto.WinnerTeamId,
                dto.DurationSeconds,
                dto.TeamAKills,
                dto.TeamBKills,
                dto.DotaMatchId,
                domainStats,
                dto.FirstPickTeamId,
                dto.TeamABanHeroIds,
                dto.TeamBBanHeroIds,
                dto.TeamARoshanKills,
                dto.TeamBRoshanKills,
                dto.TeamAAegisStolen,
                dto.TeamBAegisStolen
            );
        }
        else
        {
            series.RecordGameReplay(
                dto.WinnerTeamId,
                dto.DurationSeconds,
                dto.TeamAKills,
                dto.TeamBKills,
                dto.DotaMatchId,
                domainStats,
                dto.FirstPickTeamId,
                dto.TeamABanHeroIds,
                dto.TeamBBanHeroIds,
                dto.GameNumber,
                dto.TeamARoshanKills,
                dto.TeamBRoshanKills,
                dto.TeamAAegisStolen,
                dto.TeamBAegisStolen
            );
        }

        await _matchRepository.UpdateAsync(series, cancellationToken);

        // Se a série foi concluída, avança o vencedor/perdedor para o próximo slot do mata-mata
        if (_progressionService != null)
        {
            await _progressionService.ProgressBracketAsync(series, cancellationToken);
        }
    }
}
