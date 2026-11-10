using Identity.Application.Features.Members;
using Identity.Application.Features.Members.GetMembers;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Repositories;
using Xunit;

namespace Tournaments.UnitTests;

public class MemberFeatureTests
{
    [Fact]
    public void MemberMapper_ShouldMapUserToMemberDto_WithFriendId()
    {
        // SteamID64: 76561198052079950 => Dota Friend ID (SteamID32): 91814222
        var user = new User("miracle@dota.local", "hash123", "Amer Al-Barkawi", UserRole.User);
        user.LinkSteamAccount(76561198052079950UL, "Miracle-", "https://avatar.url/miracle.jpg", 80, 15);
        user.SetRoles([DotaRole.Carry, DotaRole.Mid], DotaRole.Mid);

        var dto = user.ToMemberDto();

        Assert.Equal("miracle@dota.local", dto.Email);
        Assert.Equal("Amer Al-Barkawi", dto.DisplayName);
        Assert.Equal("Miracle-", dto.PersonaName);
        Assert.Equal("https://avatar.url/miracle.jpg", dto.AvatarUrl);
        Assert.Equal("76561198052079950", dto.SteamId);
        Assert.Equal("https://steamcommunity.com/profiles/76561198052079950", dto.SteamProfileUrl);
        Assert.Equal("91814222", dto.DotaFriendId);
        Assert.Equal(80, dto.RankTier);
        Assert.Equal(15, dto.LeaderboardRank);
        Assert.Equal("Mid", dto.PreferredRole);
        Assert.Contains("Carry", dto.Roles);
        Assert.Equal("User", dto.Role);
        Assert.True(dto.IsSteamLocked);
        Assert.True(dto.IsActive);
        Assert.True(dto.IsTournamentEligible);
    }

    [Fact]
    public async Task GetMembersHandler_ShouldFilterAndOrderCorrectly()
    {
        var repo = new InMemoryUserRepository();

        var u1 = new User("herald@dota.local", "hash1", "NoobSlayer", UserRole.User);
        u1.LinkSteamAccount(76561198000000001UL, "Noob", null, 12, null); // Herald 2
        u1.SetRoles([DotaRole.HardSupport], DotaRole.HardSupport);

        var u2 = new User("divine@dota.local", "hash2", "ProCarry", UserRole.User);
        u2.LinkSteamAccount(76561198000000002UL, "CarryGod", null, 73, null); // Divine 3
        u2.SetRoles([DotaRole.Carry], DotaRole.Carry);

        var u3 = new User("unlinked@dota.local", "hash3", "CasualGamer", UserRole.User);
        u3.SetRoles([DotaRole.Mid], DotaRole.Mid);

        await repo.AddAsync(u1);
        await repo.AddAsync(u2);
        await repo.AddAsync(u3);

        var handler = new GetMembersHandler(repo);

        // 1. Sem filtro: Todos ordenados por rank desc (Divine -> Herald -> Casual)
        var allResult = await handler.Handle(new GetMembersQuery(), CancellationToken.None);
        Assert.True(allResult.IsSuccess);
        Assert.Equal(3, allResult.Value.Count);
        Assert.Equal("ProCarry", allResult.Value[0].DisplayName);
        Assert.Equal("NoobSlayer", allResult.Value[1].DisplayName);
        Assert.Equal("CasualGamer", allResult.Value[2].DisplayName);

        // 2. Filtro com Steam
        var steamOnlyResult = await handler.Handle(new GetMembersQuery(HasSteam: true), CancellationToken.None);
        Assert.Equal(2, steamOnlyResult.Value.Count);

        // 3. Filtro por Role "Carry"
        var carryResult = await handler.Handle(new GetMembersQuery(Role: "Carry"), CancellationToken.None);
        Assert.Single(carryResult.Value);
        Assert.Equal("ProCarry", carryResult.Value[0].DisplayName);

        // 4. Filtro por Search "Noob"
        var searchResult = await handler.Handle(new GetMembersQuery(Search: "Noob"), CancellationToken.None);
        Assert.Single(searchResult.Value);
        Assert.Equal("NoobSlayer", searchResult.Value[0].DisplayName);
    }
}
