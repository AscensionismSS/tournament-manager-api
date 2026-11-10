using Tournaments.Domain.Entities;
using Tournaments.Domain.ValueObjects;

namespace Tournaments.Domain.Services;

public static class PlayoffBracketGenerator
{
    public static IReadOnlyList<MatchSeries> Generate(
        Tournament tournament,
        IReadOnlyList<TournamentTeam> rankedTeams)
    {
        var slots = tournament.BracketSlots;
        var seriesList = new List<MatchSeries>();

        // Mapeia seeds ordinais ("1st", "2nd", etc.) para os times correspondentes
        var seedMap = new Dictionary<string, TournamentTeam>(StringComparer.OrdinalIgnoreCase);
        string[] ordinals = ["1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th"];
        for (int i = 0; i < rankedTeams.Count && i < ordinals.Length; i++)
        {
            seedMap[ordinals[i]] = rankedTeams[i];
        }

        foreach (var slot in slots)
        {
            var format = slot.Stage == BracketStage.GrandFinal
                ? tournament.SeriesConfig.GrandFinalFormat
                : tournament.SeriesConfig.PlayoffFormat;

            var series = new MatchSeries(tournament.Id, slot.Code, format);

            if (seedMap.TryGetValue(slot.TeamASource, out var teamA))
            {
                series.AssignTeamA(teamA.Id, teamA.Name);
            }

            if (seedMap.TryGetValue(slot.TeamBSource, out var teamB))
            {
                series.AssignTeamB(teamB.Id, teamB.Name);
            }

            seriesList.Add(series);
        }

        return seriesList;
    }
}
