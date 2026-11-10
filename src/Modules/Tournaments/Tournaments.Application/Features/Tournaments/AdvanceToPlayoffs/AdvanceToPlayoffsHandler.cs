using BuildingBlocks.Domain;
using MediatR;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;
using Tournaments.Domain.Services;

namespace Tournaments.Application.Features.Tournaments.AdvanceToPlayoffs;

public class AdvanceToPlayoffsHandler : IRequestHandler<AdvanceToPlayoffsCommand, TournamentDto>
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly ITournamentRealtimeNotifier? _notifier;

    public AdvanceToPlayoffsHandler(
        ITournamentRepository tournamentRepository,
        IMatchSeriesRepository matchRepository,
        ITournamentRealtimeNotifier? notifier = null)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _notifier = notifier;
    }

    public async Task<TournamentDto> Handle(AdvanceToPlayoffsCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken)
            ?? throw new DomainException($"Torneio '{request.TournamentId}' não encontrado.");

        if (tournament.IsResultSubmissionLocked)
            throw new DomainException($"O envio e alteração de resultados para este campeonato está bloqueado pela administração. Motivo: {tournament.ResultLockReason ?? "Não informado"}.");

        if (tournament.Status != TournamentStatus.GroupStage)
            throw new DomainException("Apenas torneios na Fase de Grupos podem avançar para os Playoffs.");

        var matches = await _matchRepository.GetByTournamentIdAsync(request.TournamentId, cancellationToken);
        if (matches.Any(m => m.Status != MatchSeriesStatus.Completed))
            throw new DomainException("Todas as partidas da Fase de Grupos devem estar concluídas antes de gerar os Playoffs.");

        // 1. Calcula a classificação final dos grupos aplicando os desempates oficiais
        var standings = GroupStageStandingsCalculator.Calculate(tournament.Teams.ToList(), matches);
        var rankedTeams = standings
            .Select(s => tournament.Teams.First(t => t.Id == s.TeamId))
            .ToList();

        // 2. Monta os slots de chaveamento dos Playoffs no agregado Tournament
        tournament.GeneratePlayoffs();

        // 3. Gera as entidades MatchSeries de Playoff já preenchendo os seeds iniciais (1st vs 2nd, etc.)
        var playoffMatches = PlayoffBracketGenerator.Generate(tournament, rankedTeams);
        await _matchRepository.AddRangeAsync(playoffMatches, cancellationToken);

        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

        if (_notifier != null)
        {
            await _notifier.NotifyTournamentWebhookAsync(new TournamentWebhookEventDto(
                tournament.Id,
                tournament.Name,
                "PlayoffsGenerated",
                $"A Fase de Grupos terminou! As chaves dos Playoffs de '{tournament.Name}' foram geradas.",
                tournament.Version,
                tournament.Status.ToString(),
                null,
                null,
                null,
                tournament.LastUpdatedAtUtc), cancellationToken);
        }

        return TournamentMapper.ToDto(tournament);
    }
}
