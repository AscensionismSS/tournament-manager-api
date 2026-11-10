using BuildingBlocks.Domain;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;

namespace Tournaments.Domain.Services;

/// <summary>
/// Domain Policy pura para cálculos e regras de turno do Snake Draft.
/// </summary>
public static class SnakeDraftCalculator
{
    /// <summary>
    /// Retorna o índice do time da vez (0-indexed) para um determinado pick (0-indexed).
    /// No Snake Draft:
    /// Rodada 0: 0, 1, ..., N-1
    /// Rodada 1: N-1, N-2, ..., 0 (pick duplo para o time N-1 na virada!)
    /// Rodada 2: 0, 1, ..., N-1
    /// Rodada 3: N-1, N-2, ..., 0
    /// </summary>
    public static int GetTeamIndexForPick(int pickIndex, int teamCount)
    {
        if (teamCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(teamCount), "O número de times deve ser maior que zero.");

        int round = pickIndex / teamCount;
        int positionInRound = pickIndex % teamCount;

        return (round % 2 == 0)
            ? positionInRound
            : (teamCount - 1) - positionInRound;
    }

    /// <summary>
    /// Total de picks da fase de titulares: 4 picks por time (o capitão já é o 1º titular, completando 5).
    /// </summary>
    public static int GetTotalStarterPicks(int teamCount) => teamCount * 4;

    /// <summary>
    /// Retorna a rodada atual (0-indexed) para um determinado pick.
    /// </summary>
    public static int GetCurrentRound(int pickIndex, int teamCount)
    {
        if (teamCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(teamCount), "O número de times deve ser maior que zero.");

        return pickIndex / teamCount;
    }

    /// <summary>
    /// Verifica se todos os picks de titulares já foram concluídos.
    /// </summary>
    public static bool IsStarterPhaseComplete(int currentPickIndex, int teamCount) =>
        currentPickIndex >= GetTotalStarterPicks(teamCount);

    /// <summary>
    /// Aplica a ordem de turnos (Manual ou Sorteada) na lista de equipes do draft.
    /// </summary>
    public static void ApplyPickOrder(
        IReadOnlyList<DraftTeam> teams,
        PickOrderType orderType,
        IReadOnlyList<Guid>? manualOrderIds)
    {
        if (orderType == PickOrderType.Manual)
        {
            if (manualOrderIds == null || manualOrderIds.Count != teams.Count || manualOrderIds.Distinct().Count() != teams.Count)
                throw new DomainException("A ordem manual deve conter todas as equipes sem duplicidade ou omissão.");

            for (int i = 0; i < manualOrderIds.Count; i++)
            {
                var team = teams.FirstOrDefault(t => t.Id == manualOrderIds[i])
                    ?? throw new DomainException($"Equipe com ID '{manualOrderIds[i]}' não encontrada no draft.");
                team.PickOrder = i;
            }
        }
        else
        {
            var shuffled = teams.OrderBy(_ => Random.Shared.Next()).ToList();
            for (int i = 0; i < shuffled.Count; i++)
            {
                shuffled[i].PickOrder = i;
            }
        }
    }

    /// <summary>
    /// Resolve o nome efetivo de uma equipe ao renomear (com fallback automático para "Time X" livre quando vazio).
    /// </summary>
    public static string ResolveEffectiveTeamName(
        DraftTeam targetTeam,
        string? newName,
        IReadOnlyCollection<DraftTeam> allTeams)
    {
        if (!string.IsNullOrWhiteSpace(newName))
        {
            var trimmed = newName.Trim();
            if (allTeams.Any(t => t.Id != targetTeam.Id && t.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
                throw new DomainException($"Já existe uma equipe com o nome '{trimmed}'.");
            return trimmed;
        }

        var preferred = $"Time {targetTeam.PickOrder + 1}";
        if (!allTeams.Any(t => t.Id != targetTeam.Id && t.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase)))
            return preferred;

        int counter = 1;
        while (allTeams.Any(t => t.Id != targetTeam.Id && t.Name.Equals($"Time {counter}", StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
        }
        return $"Time {counter}";
    }
}

/// <summary>
/// Alias semântico explícito para a Domain Policy de turnos do Snake Draft.
/// </summary>
public static class SnakeDraftTurnCalculator
{
    public static int GetTeamIndexForPick(int pickIndex, int teamCount) =>
        SnakeDraftCalculator.GetTeamIndexForPick(pickIndex, teamCount);

    public static int GetTotalStarterPicks(int teamCount) =>
        SnakeDraftCalculator.GetTotalStarterPicks(teamCount);

    public static int GetCurrentRound(int pickIndex, int teamCount) =>
        SnakeDraftCalculator.GetCurrentRound(pickIndex, teamCount);

    public static bool IsStarterPhaseComplete(int currentPickIndex, int teamCount) =>
        SnakeDraftCalculator.IsStarterPhaseComplete(currentPickIndex, teamCount);
}
