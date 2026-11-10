using Tournaments.Contracts.Matches;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;

namespace Tournaments.Application.Features.Matches;

public static class MatchMapper
{
    public static MatchSeriesDto ToDto(this MatchSeries series)
    {
        return new MatchSeriesDto(
            series.Id,
            series.TournamentId,
            series.SlotCode,
            series.Format.ToString(),
            series.Status.ToString(),
            series.TeamAId,
            series.TeamAName,
            series.ScoreTeamA,
            series.TeamBId,
            series.TeamBName,
            series.ScoreTeamB,
            series.WinnerTeamId,
            series.LoserTeamId,
            series.Games.Select(g => new MatchGameDto(
                g.Id,
                g.GameNumber,
                g.ResultSource.ToString(),
                g.WinnerTeamId,
                g.DurationSeconds,
                g.TeamAKills,
                g.TeamBKills,
                g.IsWalkover,
                g.DotaMatchId,
                g.FirstPickTeamId,
                g.TeamABanHeroIds,
                g.TeamBBanHeroIds,
                g.PlayerStats?.Select(p => new PlayerMatchStatsDto(
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
                )).ToList(),
                g.TeamARoshanKills,
                g.TeamBRoshanKills,
                g.TeamAAegisStolen,
                g.TeamBAegisStolen
            )).ToList(),
            series.Round
        );
    }
}
