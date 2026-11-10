using System.Globalization;
using BuildingBlocks.Domain;
using Tournaments.Application.Common.Utilities;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;

namespace Tournaments.Application.Features.Matches.UploadMatchReplay;

public interface IReplayValidationService
{
    void ValidateSeriesAndTournamentState(MatchSeries series, Tournament? tournament, int? requestedGameNumber, SystemSecuritySettings? securitySettings = null);
    void ValidateCallerPermission(Guid? requestUserId, DraftTeam teamA, DraftTeam teamB, bool isAdmin = true);
    void ValidateReplayNotDuplicate(long dotaMatchId, Guid currentSeriesId, int? gameNumber, IEnumerable<MatchSeries> allTournamentMatches);
    void ValidateReplayTimestamp(long dotaMatchId, string? matchStartDateStr, Tournament? tournament);
    void ValidateTeamPlayerCounts(IReadOnlyCollection<ParseGoDotaPlayerDto> radiantPlayers, IReadOnlyCollection<ParseGoDotaPlayerDto> direPlayers);
    void ValidateRostersMatch(
        IReadOnlyList<ParseGoDotaPlayerDto> radiantPlayers,
        IReadOnlyList<ParseGoDotaPlayerDto> direPlayers,
        DraftTeam teamA,
        DraftTeam teamB);
}

public class ReplayValidationService : IReplayValidationService
{
    public void ValidateSeriesAndTournamentState(
        MatchSeries series,
        Tournament? tournament,
        int? requestedGameNumber,
        SystemSecuritySettings? securitySettings = null)
    {
        securitySettings?.EnsureResultSubmissionAllowed();

        if (series.Status == MatchSeriesStatus.Pending || !series.TeamAId.HasValue || !series.TeamBId.HasValue)
        {
            throw new DomainException("Não é possível registrar resultado em uma partida cujas equipes ainda não foram definidas pelos confrontos anteriores.");
        }

        if (requestedGameNumber.HasValue)
        {
            var reqGame = requestedGameNumber.Value;
            if (reqGame <= series.Games.Count && reqGame >= 1)
            {
                // Jogo existente: permitido mesmo com série concluída para substituição/edição
            }
            else if (reqGame == series.Games.Count + 1)
            {
                if (series.Status == MatchSeriesStatus.Completed)
                {
                    throw new DomainException($"A série #{series.Id} já foi finalizada ({series.ScoreTeamA} x {series.ScoreTeamB}). Não é possível adicionar novos jogos.");
                }
            }
            else if (reqGame > series.Games.Count + 1)
            {
                throw new DomainException($"Não é possível registrar o Jogo #{reqGame} antes de registrar o Jogo #{reqGame - 1}.");
            }
            else
            {
                throw new DomainException($"Jogo número {reqGame} não encontrado na série.");
            }
        }
        else if (series.Status == MatchSeriesStatus.Completed)
        {
            throw new DomainException($"A série #{series.Id} já foi finalizada ({series.ScoreTeamA} x {series.ScoreTeamB}). Não é possível enviar novos replays.");
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
    }

    public void ValidateCallerPermission(Guid? requestUserId, DraftTeam teamA, DraftTeam teamB, bool isAdmin = true)
    {
        if (isAdmin)
            return;

        if (!requestUserId.HasValue)
        {
            throw new DomainException("Apenas os capitães das equipes envolvidas ou administradores do torneio podem enviar o replay desta partida.");
        }

        var userId = requestUserId.Value;
        var userIdStr = userId.ToString();

        var allPlayersA = teamA.Starters.Concat(teamA.Reserves);
        var allPlayersB = teamB.Starters.Concat(teamB.Reserves);

        var isCaptainA = teamA.CaptainId == userId ||
            allPlayersA.Any(p => p.Id == teamA.CaptainId && (p.Id == userId || string.Equals(p.ExternalUserId, userIdStr, StringComparison.OrdinalIgnoreCase)));

        var isCaptainB = teamB.CaptainId == userId ||
            allPlayersB.Any(p => p.Id == teamB.CaptainId && (p.Id == userId || string.Equals(p.ExternalUserId, userIdStr, StringComparison.OrdinalIgnoreCase)));

        if (!isCaptainA && !isCaptainB)
        {
            throw new DomainException("Apenas os capitães das equipes envolvidas ou administradores do torneio podem enviar o replay desta partida.");
        }
    }

    public void ValidateReplayNotDuplicate(long dotaMatchId, Guid currentSeriesId, int? gameNumber, IEnumerable<MatchSeries> allTournamentMatches)
    {
        var isDuplicateMatch = allTournamentMatches.Any(m => m.Games.Any(g =>
            g.DotaMatchId == dotaMatchId &&
            !(m.Id == currentSeriesId && gameNumber.HasValue && g.GameNumber == gameNumber.Value)));

        if (isDuplicateMatch)
        {
            throw new DomainException($"A partida #{dotaMatchId} do Dota 2 já foi utilizada em outro confronto deste campeonato.");
        }
    }

    public void ValidateReplayTimestamp(long dotaMatchId, string? matchStartDateStr, Tournament? tournament)
    {
        if (tournament == null || string.IsNullOrWhiteSpace(matchStartDateStr))
            return;

        if (DateTime.TryParseExact(matchStartDateStr, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
            DateTime.TryParse(matchStartDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            var matchDateUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            // Tolerância de 30 dias antes da criação do campeonato
            if (matchDateUtc < tournament.CreatedAtUtc.AddDays(-30))
            {
                throw new DomainException($"O replay da partida #{dotaMatchId} ocorreu em {matchStartDateStr}, antes da criação/início do campeonato ({tournament.CreatedAtUtc:dd/MM/yyyy HH:mm} UTC).");
            }
        }
    }

    public void ValidateTeamPlayerCounts(IReadOnlyCollection<ParseGoDotaPlayerDto> radiantPlayers, IReadOnlyCollection<ParseGoDotaPlayerDto> direPlayers)
    {
        if (radiantPlayers.Count != 5 || direPlayers.Count != 5)
        {
            throw new DomainException($"O replay deve conter exatamente 5 jogadores na Radiant e 5 na Dire. (Encontrado: Radiant={radiantPlayers.Count}, Dire={direPlayers.Count})");
        }
    }

    public void ValidateRostersMatch(
        IReadOnlyList<ParseGoDotaPlayerDto> radiantPlayers,
        IReadOnlyList<ParseGoDotaPlayerDto> direPlayers,
        DraftTeam teamA,
        DraftTeam teamB)
    {
        var teamAPlayers = teamA.Starters.Concat(teamA.Reserves).ToList();
        var teamBPlayers = teamB.Starters.Concat(teamB.Reserves).ToList();

        // Hipótese 1: Team A é Radiant, Team B é Dire
        var (unmatchedRadiantA, matchedCountRadiantA) = CheckSide(radiantPlayers, teamAPlayers);
        var (unmatchedDireB, matchedCountDireB) = CheckSide(direPlayers, teamBPlayers);
        var totalMatched1 = matchedCountRadiantA + matchedCountDireB;

        if (matchedCountRadiantA == radiantPlayers.Count && matchedCountDireB == direPlayers.Count)
        {
            return;
        }

        // Hipótese 2: Team B é Radiant, Team A é Dire
        var (unmatchedRadiantB, matchedCountRadiantB) = CheckSide(radiantPlayers, teamBPlayers);
        var (unmatchedDireA, matchedCountDireA) = CheckSide(direPlayers, teamAPlayers);
        var totalMatched2 = matchedCountRadiantB + matchedCountDireA;

        if (matchedCountRadiantB == radiantPlayers.Count && matchedCountDireA == direPlayers.Count)
        {
            return;
        }

        // Validação estrita falhou: seleciona a orientação com maior proximidade para detalhar os erros
        List<string> unmatchedDescriptions;
        if (totalMatched1 >= totalMatched2)
        {
            unmatchedDescriptions = FormatUnmatchedPlayers(unmatchedRadiantA, teamA.Name)
                .Concat(FormatUnmatchedPlayers(unmatchedDireB, teamB.Name))
                .ToList();
        }
        else
        {
            unmatchedDescriptions = FormatUnmatchedPlayers(unmatchedRadiantB, teamB.Name)
                .Concat(FormatUnmatchedPlayers(unmatchedDireA, teamA.Name))
                .ToList();
        }

        var details = unmatchedDescriptions.Count == 1
            ? $"O jogador {unmatchedDescriptions[0]} não pertence à escalação da equipe."
            : $"Os seguintes jogadores não pertencem à escalação: {string.Join(", ", unmatchedDescriptions)}.";

        throw new DomainException(
            $"O elenco do replay não corresponde aos times inscritos na série '{teamA.Name}' vs '{teamB.Name}'. {details}");
    }

    private static (List<ParseGoDotaPlayerDto> Unmatched, int MatchedCount) CheckSide(
        IReadOnlyList<ParseGoDotaPlayerDto> sidePlayers,
        IReadOnlyList<DraftPlayer> rosterPlayers)
    {
        var matchedRosterIds = new HashSet<Guid>();
        var unmatched = new List<ParseGoDotaPlayerDto>();

        foreach (var p in sidePlayers)
        {
            var match = rosterPlayers.FirstOrDefault(rp =>
                !matchedRosterIds.Contains(rp.Id) &&
                SteamIdConverter.Matches(rp.ExternalUserId, p.SteamId));

            if (match != null)
            {
                matchedRosterIds.Add(match.Id);
            }
            else
            {
                unmatched.Add(p);
            }
        }

        return (unmatched, matchedRosterIds.Count);
    }

    private static IEnumerable<string> FormatUnmatchedPlayers(
        IEnumerable<ParseGoDotaPlayerDto> players,
        string expectedTeamName)
    {
        return players.Select(p =>
        {
            var idOrName = string.IsNullOrWhiteSpace(p.Nome)
                ? $"SteamID: {p.SteamId}"
                : $"'{p.Nome}' (SteamID: {p.SteamId})";

            return $"{idOrName} (esperado em '{expectedTeamName}')";
        });
    }
}

