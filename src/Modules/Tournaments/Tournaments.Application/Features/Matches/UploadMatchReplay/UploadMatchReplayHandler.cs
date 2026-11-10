using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Application.Features.Matches.RecordReplayResult;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Repositories;

namespace Tournaments.Application.Features.Matches.UploadMatchReplay;

public class UploadMatchReplayHandler : IRequestHandler<UploadMatchReplayCommand, UploadMatchReplayResult>
{
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly IDraftSessionRepository _draftSessionRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IDotaReplayParserService _parserService;
    private readonly ISender _sender;
    private readonly IReplayValidationService _validationService;
    private readonly IReplayRosterMatcherService _rosterMatcherService;
    private readonly ISystemSecuritySettingsRepository _securitySettingsRepository;

    public UploadMatchReplayHandler(
        IMatchSeriesRepository matchRepository,
        IDraftSessionRepository draftSessionRepository,
        ITournamentRepository tournamentRepository,
        IDotaReplayParserService parserService,
        ISender sender,
        IReplayValidationService? validationService = null,
        IReplayRosterMatcherService? rosterMatcherService = null,
        ISystemSecuritySettingsRepository? securitySettingsRepository = null)
    {
        _matchRepository = matchRepository;
        _draftSessionRepository = draftSessionRepository;
        _tournamentRepository = tournamentRepository;
        _parserService = parserService;
        _sender = sender;
        _validationService = validationService ?? new ReplayValidationService();
        _rosterMatcherService = rosterMatcherService ?? new ReplayRosterMatcherService();
        _securitySettingsRepository = securitySettingsRepository ?? new DefaultSystemSecuritySettingsRepository();
    }

    public async Task<UploadMatchReplayResult> Handle(UploadMatchReplayCommand request, CancellationToken cancellationToken)
    {
        var securitySettings = await _securitySettingsRepository.GetAsync(cancellationToken);
        securitySettings.EnsureResultSubmissionAllowed();

        var series = await _matchRepository.GetByIdAsync(request.MatchSeriesId, cancellationToken)
            ?? throw new DomainException($"Série de partida '{request.MatchSeriesId}' não encontrada.");

        var tournament = await _tournamentRepository.GetByIdAsync(series.TournamentId, cancellationToken);

        // 1. Valida estado da série, jogo sequencial e status do torneio
        _validationService.ValidateSeriesAndTournamentState(series, tournament, request.GameNumber, securitySettings);

        var teamAId = series.TeamAId!.Value;
        var teamBId = series.TeamBId!.Value;

        // 2. Busca os times e jogadores da sessão de draft e valida permissão
        var draft = await _draftSessionRepository.GetByTournamentIdAsync(series.TournamentId, cancellationToken)
            ?? throw new DomainException($"Sessão de draft não encontrada para o torneio '{series.TournamentId}'.");

        var teamA = draft.Teams.FirstOrDefault(t => t.Id == teamAId)
            ?? throw new DomainException($"Equipe '{series.TeamAName}' (ID: {teamAId}) não encontrada no torneio.");
        var teamB = draft.Teams.FirstOrDefault(t => t.Id == teamBId)
            ?? throw new DomainException($"Equipe '{series.TeamBName}' (ID: {teamBId}) não encontrada no torneio.");

        _validationService.ValidateCallerPermission(request.RequestUserId, teamA, teamB, request.IsAdmin);

        // 3. Executa o parser Go isolado
        var parsed = await _parserService.ParseReplayAsync(request.ReplayStream, request.FileName, cancellationToken);
        var summary = parsed.Sumario
            ?? throw new DomainException("O resumo da partida não foi retornado pelo parser.");
        var playersReport = parsed.JogadoresEEventos
            ?? throw new DomainException("As estatísticas dos jogadores não foram retornadas pelo parser.");

        // 4. Valida anti-replay duplicado, janela temporal, contagem de jogadores e elenco estrito
        var allTournamentMatches = await _matchRepository.GetByTournamentIdAsync(series.TournamentId, cancellationToken);
        _validationService.ValidateReplayNotDuplicate(summary.MatchId, series.Id, request.GameNumber, allTournamentMatches);
        _validationService.ValidateReplayTimestamp(summary.MatchId, summary.DataHoraInicio, tournament);
        _validationService.ValidateTeamPlayerCounts(playersReport.Radiant, playersReport.Dire);
        _validationService.ValidateRostersMatch(playersReport.Radiant, playersReport.Dire, teamA, teamB);

        // 5. Reconciliação defensiva de HeroId via Picks do Draft
        _rosterMatcherService.ReconcileHeroIdsFromDraft(playersReport.Radiant, summary.PicksBans, "Radiant");
        _rosterMatcherService.ReconcileHeroIdsFromDraft(playersReport.Dire, summary.PicksBans, "Dire");

        // 6. Determina lados (Radiant/Dire) e vencedor
        var teamAPlayers = teamA.Starters.Concat(teamA.Reserves).ToList();
        var teamBPlayers = teamB.Starters.Concat(teamB.Reserves).ToList();
        var isTeamARadiant = _rosterMatcherService.ResolveIsTeamARadiant(
            playersReport.Radiant, playersReport.Dire, teamAPlayers, teamBPlayers);

        var winnerIsRadiant = string.Equals(summary.Vencedor, "Radiant", StringComparison.OrdinalIgnoreCase);
        var winnerTeamId = (isTeamARadiant == winnerIsRadiant) ? teamA.Id : teamB.Id;
        var winnerTeamName = winnerTeamId == teamA.Id ? teamA.Name : teamB.Name;

        // 7. Mapeia estatísticas detalhadas eliminando duplicação entre Radiant e Dire
        var radiantRoster = isTeamARadiant ? teamAPlayers : teamBPlayers;
        var direRoster = isTeamARadiant ? teamBPlayers : teamAPlayers;
        var radiantAssignedTeamId = isTeamARadiant ? teamA.Id : teamB.Id;
        var direAssignedTeamId = isTeamARadiant ? teamB.Id : teamA.Id;

        var statsList = new List<PlayerMatchStatsDto>(10);
        statsList.AddRange(_rosterMatcherService.MapSidePlayers(playersReport.Radiant, radiantRoster, radiantAssignedTeamId));
        statsList.AddRange(_rosterMatcherService.MapSidePlayers(playersReport.Dire, direRoster, direAssignedTeamId));

        // 8. Placar de kills, Bans, First Pick e eventos de Roshan/Aegis
        var radiantScore = summary.PlacarRadiant > 0 ? summary.PlacarRadiant : playersReport.Radiant.Sum(p => p.Kills);
        var direScore = summary.PlacarDire > 0 ? summary.PlacarDire : playersReport.Dire.Sum(p => p.Kills);
        var teamAKills = isTeamARadiant ? radiantScore : direScore;
        var teamBKills = isTeamARadiant ? direScore : radiantScore;

        var gameNumber = request.GameNumber ?? (series.Games.Count + 1);
        var durationSeconds = (long)Math.Round(summary.DuracaoSegundos);

        var (teamABans, teamBBans, firstPickTeamId) = _rosterMatcherService.ExtractDraftBansAndFirstPick(
            parsed, summary, isTeamARadiant, teamA.Id, teamB.Id);

        var (teamARoshanKills, teamBRoshanKills, teamAAegisStolen, teamBAegisStolen) =
            _rosterMatcherService.AggregateRoshanAndAegisEvents(playersReport.RoshanKills, isTeamARadiant);

        // 9. Ingestão oficial pelo RecordReplayResultCommand
        var ingestionDto = new MatchResultIngestionDto(
            series.Id,
            gameNumber,
            winnerTeamId,
            durationSeconds,
            teamAKills,
            teamBKills,
            summary.MatchId,
            statsList,
            firstPickTeamId,
            teamABans,
            teamBBans,
            teamARoshanKills,
            teamBRoshanKills,
            teamAAegisStolen,
            teamBAegisStolen
        );

        await _sender.Send(new RecordReplayResultCommand(ingestionDto), cancellationToken);

        var updatedSeries = await _matchRepository.GetByIdAsync(series.Id, cancellationToken);

        return new UploadMatchReplayResult(
            summary.MatchId,
            winnerTeamId,
            winnerTeamName,
            gameNumber,
            durationSeconds,
            updatedSeries?.ScoreTeamA ?? 0,
            updatedSeries?.ScoreTeamB ?? 0,
            updatedSeries?.Status.ToString() ?? "InProgress",
            $"Replay da partida #{summary.MatchId} processado com sucesso pelo ParseGoDota. {winnerTeamName} venceu o Jogo #{gameNumber}."
        );
    }
}
