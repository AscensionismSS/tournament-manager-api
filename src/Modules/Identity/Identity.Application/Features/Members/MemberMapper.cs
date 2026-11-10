using Identity.Contracts.Members;
using Identity.Domain.Entities;

namespace Identity.Application.Features.Members;

public static class MemberMapper
{
    private const ulong SteamIdOffset = 76561197960265728UL;

    public static MemberDto ToMemberDto(this User user)
    {
        string? dotaFriendId = null;
        string? steamIdStr = null;

        string? steamProfileUrl = null;

        if (user.SteamId.HasValue && user.SteamId.Value > 0)
        {
            ulong steam64 = user.SteamId.Value;
            steamIdStr = steam64.ToString();
            steamProfileUrl = $"https://steamcommunity.com/profiles/{steam64}";

            if (steam64 > SteamIdOffset)
            {
                uint friendId = (uint)(steam64 - SteamIdOffset);
                dotaFriendId = friendId.ToString();
            }
            else
            {
                dotaFriendId = steam64.ToString();
            }
        }

        return new MemberDto(
            UserId: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            PersonaName: user.DotaPersonaName,
            AvatarUrl: user.DotaAvatarUrl,
            SteamId: steamIdStr,
            SteamProfileUrl: steamProfileUrl,
            DotaFriendId: dotaFriendId,
            RankTier: user.DotaRankTier,
            LeaderboardRank: user.DotaLeaderboardRank,
            Roles: user.Roles.Select(r => r.ToString()).ToList(),
            PreferredRole: user.PreferredRole?.ToString(),
            Role: user.Role.ToString(),
            IsSteamLocked: user.IsSteamLocked,
            IsActive: user.IsActive,
            StatusReason: user.StatusReason,
            IsTournamentEligible: user.IsTournamentEligible
        );
    }
}
