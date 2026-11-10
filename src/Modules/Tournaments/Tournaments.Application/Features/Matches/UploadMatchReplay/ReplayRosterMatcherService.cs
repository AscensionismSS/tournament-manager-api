using BuildingBlocks.Domain;
using Tournaments.Application.Common.Utilities;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;

namespace Tournaments.Application.Features.Matches.UploadMatchReplay;

public interface IReplayRosterMatcherService
{
    void ReconcileHeroIdsFromDraft(List<ParseGoDotaPlayerDto> players, List<ParseGoDotaPickBanDto>? picksBans, string teamLabel);
    bool ResolveIsTeamARadiant(
        IReadOnlyList<ParseGoDotaPlayerDto> radiantPlayers,
        IReadOnlyList<ParseGoDotaPlayerDto> direPlayers,
        IReadOnlyList<DraftPlayer> teamAPlayers,
        IReadOnlyList<DraftPlayer> teamBPlayers);
    List<PlayerMatchStatsDto> MapSidePlayers(
        IReadOnlyList<ParseGoDotaPlayerDto> sidePlayers,
        IReadOnlyList<DraftPlayer> teamRoster,
        Guid assignedTeamId);
    (List<int> TeamABans, List<int> TeamBBans, Guid? FirstPickTeamId) ExtractDraftBansAndFirstPick(
        ParseGoDotaOutputDto parsed,
        ParseGoDotaSummaryDto summary,
        bool isTeamARadiant,
        Guid teamAId,
        Guid teamBId);
    (int TeamARoshanKills, int TeamBRoshanKills, int TeamAAegisStolen, int TeamBAegisStolen) AggregateRoshanAndAegisEvents(
        IReadOnlyList<ParseGoDotaRoshanKillDto> roshanKills,
        bool isTeamARadiant);
}

public class ReplayRosterMatcherService : IReplayRosterMatcherService
{
    public void ReconcileHeroIdsFromDraft(List<ParseGoDotaPlayerDto> players, List<ParseGoDotaPickBanDto>? picksBans, string teamLabel)
    {
        if (picksBans == null || picksBans.Count == 0)
            return;

        var teamPicks = picksBans
            .Where(pb => string.Equals(pb.Acao, "PICK", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(pb.Time, teamLabel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (teamPicks.Count == 0) return;

        var assignedHeroIds = players.Where(p => p.HeroId > 0).Select(p => p.HeroId).ToHashSet();
        var unassignedPicks = teamPicks.Where(p => !assignedHeroIds.Contains(p.HeroId)).ToList();

        var unassignedIdx = 0;
        foreach (var player in players)
        {
            if (player.HeroId == 0 && unassignedIdx < unassignedPicks.Count)
            {
                player.HeroId = unassignedPicks[unassignedIdx].HeroId;
                if (string.IsNullOrEmpty(player.Heroi) || player.Heroi.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    player.Heroi = unassignedPicks[unassignedIdx].Heroi;
                }
                unassignedIdx++;
            }
        }
    }

    public bool ResolveIsTeamARadiant(
        IReadOnlyList<ParseGoDotaPlayerDto> radiantPlayers,
        IReadOnlyList<ParseGoDotaPlayerDto> direPlayers,
        IReadOnlyList<DraftPlayer> teamAPlayers,
        IReadOnlyList<DraftPlayer> teamBPlayers)
    {
        var radiantMatchesA = radiantPlayers.Count(rp => teamAPlayers.Any(p => SteamIdConverter.Matches(p.ExternalUserId, rp.SteamId)));
        var direMatchesB = direPlayers.Count(dp => teamBPlayers.Any(p => SteamIdConverter.Matches(p.ExternalUserId, dp.SteamId)));

        if (radiantMatchesA == 5 && direMatchesB == 5)
            return true;

        var radiantMatchesB = radiantPlayers.Count(rp => teamBPlayers.Any(p => SteamIdConverter.Matches(p.ExternalUserId, rp.SteamId)));
        var direMatchesA = direPlayers.Count(dp => teamAPlayers.Any(p => SteamIdConverter.Matches(p.ExternalUserId, dp.SteamId)));

        if (radiantMatchesB == 5 && direMatchesA == 5)
            return false;

        throw new DomainException("O elenco do replay não corresponde aos times inscritos na série.");
    }

    public List<PlayerMatchStatsDto> MapSidePlayers(
        IReadOnlyList<ParseGoDotaPlayerDto> sidePlayers,
        IReadOnlyList<DraftPlayer> teamRoster,
        Guid assignedTeamId)
    {
        var assignedPlayerIds = new HashSet<Guid>();
        var matched = new (ParseGoDotaPlayerDto ReplayPlayer, DraftPlayer RosterPlayer)[sidePlayers.Count];

        for (int i = 0; i < sidePlayers.Count; i++)
        {
            var rp = sidePlayers[i];
            var match = teamRoster.FirstOrDefault(p =>
                !assignedPlayerIds.Contains(p.Id) &&
                SteamIdConverter.Matches(p.ExternalUserId, rp.SteamId));

            if (match == null)
            {
                var idOrName = string.IsNullOrWhiteSpace(rp.Nome)
                    ? $"SteamID: {rp.SteamId}"
                    : $"'{rp.Nome}' (SteamID: {rp.SteamId})";

                throw new DomainException($"O jogador {idOrName} não pertence ao elenco registrado da equipe.");
            }

            assignedPlayerIds.Add(match.Id);
            matched[i] = (rp, match);
        }

        var result = new List<PlayerMatchStatsDto>(sidePlayers.Count);

        for (int i = 0; i < sidePlayers.Count; i++)
        {
            var (rp, match) = matched[i];

            result.Add(new PlayerMatchStatsDto(
                match.Id,
                assignedTeamId,
                rp.HeroId,
                rp.Kills,
                rp.Deaths,
                rp.Assists,
                rp.NetWorth,
                rp.Gpm,
                rp.Xpm,
                null,
                PlayerName: match.Name,
                SteamName: rp.Nome,
                HeroName: rp.Heroi,
                ItemNames: rp.Inventario?.ItensPrincipais,
                BackpackNames: rp.Inventario?.Mochila,
                NeutralItem: rp.Inventario?.ItemNeutro,
                NeutralItems: rp.Inventario?.ItensNeutros,
                JungleBuff: rp.Inventario?.BuffJungle,
                JungleBuffs: rp.Inventario?.BuffsJungle,
                HasAghanimScepter: rp.Upgrades?.AghanimCetro ?? false,
                HasAghanimShard: rp.Upgrades?.AghanimShard ?? false,
                WardsPlaced: rp.WardsObsCompradas + rp.WardsSenCompradas,
                WardsDestroyed: rp.WardsObsDestruidas,
                TowerDamage: rp.DanoEstruturas,
                HeroDamage: rp.DanoHerois,
                HeroHealing: rp.Cura
            ));
        }

        return result;
    }

    public (List<int> TeamABans, List<int> TeamBBans, Guid? FirstPickTeamId) ExtractDraftBansAndFirstPick(
        ParseGoDotaOutputDto parsed,
        ParseGoDotaSummaryDto summary,
        bool isTeamARadiant,
        Guid teamAId,
        Guid teamBId)
    {
        var radiantBans = parsed.RadiantBans.Count > 0
            ? parsed.RadiantBans
            : (summary.RadiantBans != null && summary.RadiantBans.Count > 0
                ? summary.RadiantBans
                : summary.PicksBans?
                    .Where(pb => pb.Acao == "BAN" && string.Equals(pb.Time, "Radiant", StringComparison.OrdinalIgnoreCase))
                    .Select(pb => pb.HeroId)
                    .ToList() ?? []);

        var direBans = parsed.DireBans.Count > 0
            ? parsed.DireBans
            : (summary.DireBans != null && summary.DireBans.Count > 0
                ? summary.DireBans
                : summary.PicksBans?
                    .Where(pb => pb.Acao == "BAN" && string.Equals(pb.Time, "Dire", StringComparison.OrdinalIgnoreCase))
                    .Select(pb => pb.HeroId)
                    .ToList() ?? []);

        var firstPickStr = parsed.FirstPick ?? summary.FirstPick;
        if (string.IsNullOrEmpty(firstPickStr) && summary.PicksBans?.Count > 0)
        {
            firstPickStr = summary.PicksBans.FirstOrDefault(pb => pb.Acao == "PICK")?.Time;
        }

        var teamABans = isTeamARadiant ? radiantBans : direBans;
        var teamBBans = isTeamARadiant ? direBans : radiantBans;

        Guid? firstPickTeamId = null;
        if (!string.IsNullOrEmpty(firstPickStr))
        {
            var isFirstPickRadiant = firstPickStr.Equals("Radiant", StringComparison.OrdinalIgnoreCase);
            firstPickTeamId = (isTeamARadiant == isFirstPickRadiant) ? teamAId : teamBId;
        }

        return (teamABans, teamBBans, firstPickTeamId);
    }

    public (int TeamARoshanKills, int TeamBRoshanKills, int TeamAAegisStolen, int TeamBAegisStolen) AggregateRoshanAndAegisEvents(
        IReadOnlyList<ParseGoDotaRoshanKillDto> roshanKills,
        bool isTeamARadiant)
    {
        var teamARoshanKills = 0;
        var teamBRoshanKills = 0;
        var teamAAegisStolen = 0;
        var teamBAegisStolen = 0;

        foreach (var rk in roshanKills)
        {
            var killerTeam = rk.DerrotadoPor?.Time;
            var isKillerRadiant = string.Equals(killerTeam, "Radiant", StringComparison.OrdinalIgnoreCase);
            var isKillerDire = string.Equals(killerTeam, "Dire", StringComparison.OrdinalIgnoreCase);

            if (isKillerRadiant)
            {
                if (isTeamARadiant) teamARoshanKills++;
                else teamBRoshanKills++;
            }
            else if (isKillerDire)
            {
                if (isTeamARadiant) teamBRoshanKills++;
                else teamARoshanKills++;
            }

            if (rk.FoiRoubado)
            {
                var takenTeam = rk.AegisPegoPor?.Time;
                var isTakenRadiant = string.Equals(takenTeam, "Radiant", StringComparison.OrdinalIgnoreCase);
                var isTakenDire = string.Equals(takenTeam, "Dire", StringComparison.OrdinalIgnoreCase);

                if (isTakenRadiant)
                {
                    if (isTeamARadiant) teamAAegisStolen++;
                    else teamBAegisStolen++;
                }
                else if (isTakenDire)
                {
                    if (isTeamARadiant) teamBAegisStolen++;
                    else teamAAegisStolen++;
                }
            }
        }

        return (teamARoshanKills, teamBRoshanKills, teamAAegisStolen, teamBAegisStolen);
    }
}

