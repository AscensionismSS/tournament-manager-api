using BuildingBlocks.Domain;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.Tournaments;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Repositories;
using Tournaments.Domain.ValueObjects;

namespace Tournaments.Application.Common.Services;

public class BracketProgressionService : IBracketProgressionService
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IMatchSeriesRepository _matchRepository;
    private readonly ITournamentRealtimeNotifier? _notifier;

    public BracketProgressionService(
        ITournamentRepository tournamentRepository,
        IMatchSeriesRepository matchRepository,
        ITournamentRealtimeNotifier? notifier = null)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _notifier = notifier;
    }

    public async Task ProgressBracketAsync(MatchSeries completedSeries, CancellationToken cancellationToken = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(completedSeries.TournamentId, cancellationToken);
        if (tournament == null) return;

        if (completedSeries.Status != MatchSeriesStatus.Completed || !completedSeries.WinnerTeamId.HasValue)
        {
            tournament.Touch();
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
            var gameMsg = $"Novo mapa registrado em {completedSeries.TeamAName ?? "Time A"} vs {completedSeries.TeamBName ?? "Time B"} ({completedSeries.ScoreTeamA}x{completedSeries.ScoreTeamB}).";
            await EmitWebhookAsync(tournament, "MatchGameRecorded", gameMsg, completedSeries.Id, completedSeries.SlotCode, null, cancellationToken);
            return;
        }

        var winnerId = completedSeries.WinnerTeamId.Value;
        var winnerName = (winnerId == completedSeries.TeamAId ? completedSeries.TeamAName : completedSeries.TeamBName) ?? "Vencedor";
        var defeatedName = (winnerId == completedSeries.TeamAId ? completedSeries.TeamBName : completedSeries.TeamAName) ?? "Adversário";

        var currentSlot = tournament.BracketSlots.FirstOrDefault(s => s.Code.Equals(completedSeries.SlotCode, StringComparison.OrdinalIgnoreCase));
        if (currentSlot == null)
        {
            // Partida de fase de grupos não possui progressão topológica de bracket, mas atualiza a versão da tabela
            tournament.Touch();
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
            var groupMsg = $"Partida de grupo encerrada: {winnerName} venceu {defeatedName} ({completedSeries.ScoreTeamA}x{completedSeries.ScoreTeamB})!";
            await EmitWebhookAsync(tournament, "GroupMatchCompleted", groupMsg, completedSeries.Id, completedSeries.SlotCode, winnerName, cancellationToken);
            return;
        }

        // Se for a Grande Final (GF), coroa o campeão!
        if (currentSlot.Stage == BracketStage.GrandFinal || currentSlot.Code.Equals("GF", StringComparison.OrdinalIgnoreCase))
        {
            tournament.CrownChampion(winnerId, winnerName);
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
            var champMsg = $"👑 {winnerName} venceu a Grande Final e é o Campeão de '{tournament.Name}'!";
            await EmitWebhookAsync(tournament, "TournamentCompleted", champMsg, completedSeries.Id, completedSeries.SlotCode, winnerName, cancellationToken);
            return;
        }

        var allMatches = await _matchRepository.GetByTournamentIdAsync(completedSeries.TournamentId, cancellationToken);

        // 1. Promove o vencedor para WinnerTargetSlotCode
        if (!string.IsNullOrEmpty(currentSlot.WinnerTargetSlotCode))
        {
            var targetSlot = tournament.BracketSlots.FirstOrDefault(s => s.Code.Equals(currentSlot.WinnerTargetSlotCode, StringComparison.OrdinalIgnoreCase));
            var targetMatch = allMatches.FirstOrDefault(m => m.SlotCode.Equals(currentSlot.WinnerTargetSlotCode, StringComparison.OrdinalIgnoreCase));

            if (targetSlot != null && targetMatch != null)
            {
                if (targetSlot.TeamASource.Equals($"Winner({currentSlot.Code})", StringComparison.OrdinalIgnoreCase))
                {
                    targetMatch.AssignTeamA(winnerId, winnerName ?? string.Empty);
                }
                else if (targetSlot.TeamBSource.Equals($"Winner({currentSlot.Code})", StringComparison.OrdinalIgnoreCase))
                {
                    targetMatch.AssignTeamB(winnerId, winnerName ?? string.Empty);
                }

                await _matchRepository.UpdateAsync(targetMatch, cancellationToken);
            }
        }

        // 2. Desce o perdedor para LoserTargetSlotCode (se houver)
        if (!string.IsNullOrEmpty(currentSlot.LoserTargetSlotCode) && completedSeries.LoserTeamId.HasValue)
        {
            var loserId = completedSeries.LoserTeamId.Value;
            var loserName = loserId == completedSeries.TeamAId ? completedSeries.TeamAName : completedSeries.TeamBName;

            var targetSlot = tournament.BracketSlots.FirstOrDefault(s => s.Code.Equals(currentSlot.LoserTargetSlotCode, StringComparison.OrdinalIgnoreCase));
            var targetMatch = allMatches.FirstOrDefault(m => m.SlotCode.Equals(currentSlot.LoserTargetSlotCode, StringComparison.OrdinalIgnoreCase));

            if (targetSlot != null && targetMatch != null)
            {
                if (targetSlot.TeamASource.Equals($"Loser({currentSlot.Code})", StringComparison.OrdinalIgnoreCase))
                {
                    targetMatch.AssignTeamA(loserId, loserName ?? string.Empty);
                }
                else if (targetSlot.TeamBSource.Equals($"Loser({currentSlot.Code})", StringComparison.OrdinalIgnoreCase))
                {
                    targetMatch.AssignTeamB(loserId, loserName ?? string.Empty);
                }

                await _matchRepository.UpdateAsync(targetMatch, cancellationToken);
            }
        }

        tournament.Touch();
        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
        var bracketMsg = $"Partida {completedSeries.SlotCode} encerrada: {winnerName} venceu {defeatedName} ({completedSeries.ScoreTeamA}x{completedSeries.ScoreTeamB}) e avançou na chave!";
        await EmitWebhookAsync(tournament, "BracketProgressed", bracketMsg, completedSeries.Id, completedSeries.SlotCode, winnerName, cancellationToken);
    }

    public async Task UndoProgressionOnlyAsync(MatchSeries seriesToUndo, CancellationToken cancellationToken = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(seriesToUndo.TournamentId, cancellationToken);
        if (tournament == null) return;

        var currentSlot = tournament.BracketSlots.FirstOrDefault(s => s.Code.Equals(seriesToUndo.SlotCode, StringComparison.OrdinalIgnoreCase));
        if (currentSlot == null)
        {
            // Partida de grupo: trava de segurança caso os Playoffs já tenham iniciado ou concluído
            if (tournament.Status == TournamentStatus.Playoffs || tournament.Status == TournamentStatus.Completed)
            {
                throw new DomainException($"Não é possível reverter a partida de grupo '{seriesToUndo.SlotCode}' pois a fase de Playoffs já foi iniciada ou concluída. Reverta a fase de Playoffs para a Fase de Grupos primeiro.");
            }

            tournament.Touch();
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
            return;
        }

        // Se era a Grande Final e o torneio já estava concluído, reverte o campeão
        if (currentSlot.Stage == BracketStage.GrandFinal || currentSlot.Code.Equals("GF", StringComparison.OrdinalIgnoreCase))
        {
            if (tournament.Status == TournamentStatus.Completed)
            {
                tournament.RevertChampion();
                await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
            }
            return;
        }

        var allMatches = await _matchRepository.GetByTournamentIdAsync(seriesToUndo.TournamentId, cancellationToken);

        // Validação de segurança: Verifica se as partidas de destino subsequentes já possuem jogos jogados
        MatchSeries? winnerTargetMatch = null;
        BracketSlot? winnerTargetSlot = null;
        if (!string.IsNullOrEmpty(currentSlot.WinnerTargetSlotCode))
        {
            winnerTargetSlot = tournament.BracketSlots.FirstOrDefault(s => s.Code.Equals(currentSlot.WinnerTargetSlotCode, StringComparison.OrdinalIgnoreCase));
            winnerTargetMatch = allMatches.FirstOrDefault(m => m.SlotCode.Equals(currentSlot.WinnerTargetSlotCode, StringComparison.OrdinalIgnoreCase));
            if (winnerTargetMatch != null && winnerTargetMatch.Games.Count > 0)
            {
                throw new DomainException($"Não é possível reverter a partida '{seriesToUndo.SlotCode}' pois a partida subsequente '{winnerTargetMatch.SlotCode}' já possui mapas jogados. Reverta a partida '{winnerTargetMatch.SlotCode}' primeiro.");
            }
        }

        MatchSeries? loserTargetMatch = null;
        BracketSlot? loserTargetSlot = null;
        if (!string.IsNullOrEmpty(currentSlot.LoserTargetSlotCode))
        {
            loserTargetSlot = tournament.BracketSlots.FirstOrDefault(s => s.Code.Equals(currentSlot.LoserTargetSlotCode, StringComparison.OrdinalIgnoreCase));
            loserTargetMatch = allMatches.FirstOrDefault(m => m.SlotCode.Equals(currentSlot.LoserTargetSlotCode, StringComparison.OrdinalIgnoreCase));
            if (loserTargetMatch != null && loserTargetMatch.Games.Count > 0)
            {
                throw new DomainException($"Não é possível reverter a partida '{seriesToUndo.SlotCode}' pois a partida subsequente '{loserTargetMatch.SlotCode}' já possui mapas jogados. Reverta a partida '{loserTargetMatch.SlotCode}' primeiro.");
            }
        }

        // Remove os times promovidos das partidas subsequentes
        if (winnerTargetMatch != null && winnerTargetSlot != null)
        {
            if (winnerTargetSlot.TeamASource.Equals($"Winner({currentSlot.Code})", StringComparison.OrdinalIgnoreCase))
            {
                winnerTargetMatch.ClearTeamA();
            }
            else if (winnerTargetSlot.TeamBSource.Equals($"Winner({currentSlot.Code})", StringComparison.OrdinalIgnoreCase))
            {
                winnerTargetMatch.ClearTeamB();
            }
            await _matchRepository.UpdateAsync(winnerTargetMatch, cancellationToken);
        }

        if (loserTargetMatch != null && loserTargetSlot != null)
        {
            if (loserTargetSlot.TeamASource.Equals($"Loser({currentSlot.Code})", StringComparison.OrdinalIgnoreCase))
            {
                loserTargetMatch.ClearTeamA();
            }
            else if (loserTargetSlot.TeamBSource.Equals($"Loser({currentSlot.Code})", StringComparison.OrdinalIgnoreCase))
            {
                loserTargetMatch.ClearTeamB();
            }
            await _matchRepository.UpdateAsync(loserTargetMatch, cancellationToken);
        }

        tournament.Touch();
        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
        var revertMsg = $"O resultado da série {seriesToUndo.SlotCode} foi revertido pelo administrador.";
        await EmitWebhookAsync(tournament, "MatchReverted", revertMsg, seriesToUndo.Id, seriesToUndo.SlotCode, null, cancellationToken);
    }

    public async Task RollbackBracketProgressionAsync(MatchSeries seriesToRollback, CancellationToken cancellationToken = default)
    {
        await UndoProgressionOnlyAsync(seriesToRollback, cancellationToken);
        seriesToRollback.RollbackResult();
        await _matchRepository.UpdateAsync(seriesToRollback, cancellationToken);
    }

    private Task EmitWebhookAsync(
        Tournament tournament,
        string eventType,
        string message,
        Guid? matchSeriesId,
        string? slotCode,
        string? winnerTeamName,
        CancellationToken ct)
    {
        if (_notifier == null) return Task.CompletedTask;

        var evt = new TournamentWebhookEventDto(
            tournament.Id,
            tournament.Name,
            eventType,
            message,
            tournament.Version,
            tournament.Status.ToString(),
            matchSeriesId,
            slotCode,
            winnerTeamName,
            tournament.LastUpdatedAtUtc);

        return _notifier.NotifyTournamentWebhookAsync(evt, ct);
    }
}
