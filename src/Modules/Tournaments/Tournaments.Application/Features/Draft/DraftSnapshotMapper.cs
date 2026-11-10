using Tournaments.Contracts.Draft;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Enums;
using Tournaments.Domain.Services;

namespace Tournaments.Application.Features.Draft;

public static class DraftSnapshotMapper
{
    public static DraftStateSnapshotDto ToSnapshotDto(DraftSession session)
    {
        Guid? currentTurnTeamId = null;
        int? currentTurnPickOrder = null;

        if (session.State == DraftState.DraftingStarters)
        {
            int totalPicks = SnakeDraftCalculator.GetTotalStarterPicks(session.TargetTeamCount);
            if (session.CurrentPickIndex < totalPicks)
            {
                currentTurnPickOrder = SnakeDraftCalculator.GetTeamIndexForPick(session.CurrentPickIndex, session.TargetTeamCount);
                var currentTeam = session.Teams.FirstOrDefault(t => t.PickOrder == currentTurnPickOrder.Value);
                currentTurnTeamId = currentTeam?.Id;
            }
        }

        var playerPoolDtos = session.PlayerPool
            .Select(p => p.ToDto())
            .ToList();

        var teamDtos = session.Teams
            .OrderBy(t => t.PickOrder)
            .Select(t => new DraftTeamDto(
                t.Id,
                t.Name,
                t.CaptainId,
                t.PickOrder,
                t.Starters.Select(p => p.ToDto()).ToList(),
                t.Reserves.Select(p => p.ToDto()).ToList()
            ))
            .ToList();

        int minRequired = session.TargetTeamCount * 5;
        int totalPlayers = session.PlayerPool.Count + session.Teams.Sum(t => (t.CaptainId.HasValue ? 1 : 0) + t.Starters.Count + t.Reserves.Count);
        bool canStartDraft = totalPlayers >= minRequired;
        bool allCaptainsAssigned = session.Teams.Count == session.TargetTeamCount && session.Teams.All(t => t.CaptainId.HasValue);
        int totalStarterPicks = SnakeDraftCalculator.GetTotalStarterPicks(session.TargetTeamCount);
        bool allStartersPicked = session.CurrentPickIndex >= totalStarterPicks;

        return new DraftStateSnapshotDto(
            session.Id,
            session.TournamentId,
            session.State.ToString(),
            session.TargetTeamCount,
            session.CurrentPickIndex,
            currentTurnTeamId,
            currentTurnPickOrder,
            session.DraftVersion,
            session.SubstitutingPlayerId,
            session.SubstitutingTeamId,
            playerPoolDtos,
            teamDtos,
            minRequired,
            canStartDraft,
            allCaptainsAssigned,
            allStartersPicked,
            totalStarterPicks
        );
    }

    public static DraftPlayerDto ToDto(this DraftPlayer player)
    {
        string? friendId = null;
        string? steamUrl = null;
        if (ulong.TryParse(player.ExternalUserId, out var steam64) && steam64 > 76561197960265728UL)
        {
            friendId = ((uint)(steam64 - 76561197960265728UL)).ToString();
            steamUrl = $"https://steamcommunity.com/profiles/{steam64}";
        }

        return new DraftPlayerDto(
            player.Id,
            player.Name,
            player.ExternalUserId,
            player.Roles,
            player.PreferredRole,
            player.RankTier,
            null,
            player.AvatarUrl,
            friendId,
            steamUrl
        );
    }
}
