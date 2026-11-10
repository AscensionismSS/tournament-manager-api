using BuildingBlocks.Domain;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;

namespace Tournaments.Domain.Services;

public static class GroupStageMatchGenerator
{
    public static IReadOnlyList<MatchSeries> Generate(Tournament tournament)
    {
        var teams = tournament.Teams.ToList();
        var format = tournament.SeriesConfig.GroupStageFormat
            ?? throw new DomainException("Torneio não possui formato de Fase de Grupos configurado.");
        var seriesList = new List<MatchSeries>();

        if (tournament.FormatConfig.GroupCount == 2 && teams.Count == 8)
        {
            // 2 Grupos de 4 equipes (Grupo A e Grupo B)
            var groupA = teams.Take(4).ToList();
            var groupB = teams.Skip(4).Take(4).ToList();

            seriesList.AddRange(GenerateRoundRobin(tournament.Id, groupA, format, "GA"));
            seriesList.AddRange(GenerateRoundRobin(tournament.Id, groupB, format, "GB"));
        }
        else
        {
            // Grupo Único (todos contra todos)
            seriesList.AddRange(GenerateRoundRobin(tournament.Id, teams, format, "G"));
        }

        return seriesList;
    }

    private static IReadOnlyList<MatchSeries> GenerateRoundRobin(
        Guid tournamentId,
        IReadOnlyList<TournamentTeam> teams,
        SeriesFormat format,
        string prefix)
    {
        var matches = new List<MatchSeries>();
        if (teams.Count < 2) return matches;

        // Se número de times for ímpar (ex: 5 ou 7), adiciona um slot nulo (BYE)
        var teamList = new List<TournamentTeam?>(teams);
        if (teamList.Count % 2 != 0)
        {
            teamList.Add(null); // Equipe de folga na rodada
        }

        int numTeams = teamList.Count;
        int numRounds = numTeams - 1;
        int halfSize = numTeams / 2;

        var currentTeams = new List<TournamentTeam?>(teamList);

        for (int round = 1; round <= numRounds; round++)
        {
            int matchInRound = 1;
            for (int i = 0; i < halfSize; i++)
            {
                var teamA = currentTeams[i];
                var teamB = currentTeams[numTeams - 1 - i];

                // Se nenhum for BYE (folga), gera o confronto oficial da rodada
                if (teamA != null && teamB != null)
                {
                    var slotCode = $"{prefix}_R{round}_M{matchInRound++}";
                    var series = new MatchSeries(tournamentId, slotCode, format, round: round);
                    series.AssignTeams(teamA.Id, teamA.Name, teamB.Id, teamB.Name);
                    matches.Add(series);
                }
            }

            // Algoritmo de Rotação de Berger: Mantém o índice 0 fixo e rotaciona os demais no sentido horário
            var rotated = new List<TournamentTeam?>(numTeams);
            rotated.Add(currentTeams[0]);
            rotated.Add(currentTeams[numTeams - 1]); // O último vai para a posição 1
            for (int j = 1; j < numTeams - 1; j++)
            {
                rotated.Add(currentTeams[j]);
            }
            currentTeams = rotated;
        }

        return matches;
    }
}
