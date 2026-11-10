using BuildingBlocks.Domain;
using Tournaments.Application.Features.Matches.UploadMatchReplay;
using Tournaments.Contracts.Replay;
using Tournaments.Domain.Entities;
using Tournaments.Domain.Services;
using Xunit;

namespace Tournaments.UnitTests;

public class ArchitectureSrpRefactoringTests
{
    [Fact]
    public void ReplayValidationService_WhenCallerIsNotCaptainAndNotAdmin_ShouldThrowDomainException()
    {
        var validator = new ReplayValidationService();
        var teamA = new DraftTeam("Time A", 0);
        var teamB = new DraftTeam("Time B", 1);

        var captainA = new DraftPlayer(Guid.NewGuid(), "CapA");
        var captainB = new DraftPlayer(Guid.NewGuid(), "CapB");
        teamA.AssignCaptain(captainA);
        teamB.AssignCaptain(captainB);

        var unauthorizedUserId = Guid.NewGuid();

        var ex = Assert.Throws<DomainException>(() =>
            validator.ValidateCallerPermission(unauthorizedUserId, teamA, teamB, isAdmin: false));
        Assert.Contains("Apenas os capitães", ex.Message);

        // Quando é capitão ou admin, deve permitir sem exceção
        validator.ValidateCallerPermission(captainA.Id, teamA, teamB, isAdmin: false);
        validator.ValidateCallerPermission(captainB.Id, teamA, teamB, isAdmin: false);
        validator.ValidateCallerPermission(unauthorizedUserId, teamA, teamB, isAdmin: true);
    }

    [Fact]
    public void ReplayRosterMatcherService_MapSidePlayers_ShouldMatchRosterAndPreserveNeutralHistory()
    {
        var matcher = new ReplayRosterMatcherService();
        var teamId = Guid.NewGuid();
        var p1Id = Guid.NewGuid();
        var roster = new List<DraftPlayer>
        {
            new(p1Id, "TsurusNick1", "76561198000000001")
        };

        var sidePlayers = new List<ParseGoDotaPlayerDto>
        {
            new()
            {
                SteamId = 76561198000000001,
                Nome = "SteamNick1",
                HeroId = 108,
                Heroi = "Underlord",
                Kills = 5,
                Deaths = 2,
                Assists = 10,
                Inventario = new ParseGoDotaInventoryDto
                {
                    ItemNeutro = "item_searing_signet",
                    ItensNeutros = ["item_trusty_shovel", "item_searing_signet"],
                    BuffJungle = "item_enhancement_brawny",
                    BuffsJungle = ["item_enhancement_tough", "item_enhancement_brawny"]
                }
            }
        };

        var stats = matcher.MapSidePlayers(sidePlayers, roster, teamId);

        Assert.Single(stats);
        Assert.Equal(p1Id, stats[0].PlayerId);
        Assert.Equal("TsurusNick1", stats[0].PlayerName);
        Assert.Equal("SteamNick1", stats[0].SteamName);
        Assert.Equal("item_searing_signet", stats[0].NeutralItem);
        Assert.Equal(2, stats[0].NeutralItems!.Count);
        Assert.Equal("item_enhancement_brawny", stats[0].JungleBuff);
        Assert.Equal(2, stats[0].JungleBuffs!.Count);
    }

    [Fact]
    public void SnakeDraftTurnCalculator_ShouldCalculateRoundAndStarterPhaseCorrectly()
    {
        Assert.Equal(16, SnakeDraftTurnCalculator.GetTotalStarterPicks(4));
        Assert.Equal(0, SnakeDraftTurnCalculator.GetCurrentRound(0, 4));
        Assert.Equal(1, SnakeDraftTurnCalculator.GetCurrentRound(4, 4));
        Assert.False(SnakeDraftTurnCalculator.IsStarterPhaseComplete(15, 4));
        Assert.True(SnakeDraftTurnCalculator.IsStarterPhaseComplete(16, 4));
    }

    [Fact]
    public void ReplayValidationService_ValidateRostersMatch_WhenReservePlays_ShouldPass()
    {
        var validator = new ReplayValidationService();
        var teamA = new DraftTeam("Equipe Alpha", 0);
        var teamB = new DraftTeam("Equipe Beta", 1);

        for (int i = 1; i <= 5; i++)
        {
            teamA.AddStarter(new DraftPlayer(Guid.NewGuid(), $"Alpha_Starter_{i}", $"7656119800000000{i}"));
            teamB.AddStarter(new DraftPlayer(Guid.NewGuid(), $"Beta_Starter_{i}", $"7656119800000001{i}"));
        }
        // Alpha tem 1 reserva
        var reservePlayer = new DraftPlayer(Guid.NewGuid(), "Alpha_Reserve_1", "76561198000000099");
        teamA.AddReserve(reservePlayer);

        // Replay: no Time Alpha (Radiant), jogam os titulares 1 a 4 + o reserva
        var radiant = new List<ParseGoDotaPlayerDto>
        {
            new() { SteamId = 76561198000000001L, Nome = "Alpha_Starter_1" },
            new() { SteamId = 76561198000000002L, Nome = "Alpha_Starter_2" },
            new() { SteamId = 76561198000000003L, Nome = "Alpha_Starter_3" },
            new() { SteamId = 76561198000000004L, Nome = "Alpha_Starter_4" },
            new() { SteamId = 76561198000000099L, Nome = "Alpha_Reserve_1" }
        };

        var dire = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto
        {
            SteamId = 76561198000000010L + i,
            Nome = $"Beta_Starter_{i}"
        }).ToList();

        // Não deve lançar exceção
        validator.ValidateRostersMatch(radiant, dire, teamA, teamB);
    }

    [Fact]
    public void ReplayValidationService_ValidateRostersMatch_WhenInvertedSides_ShouldPass()
    {
        var validator = new ReplayValidationService();
        var teamA = new DraftTeam("Equipe Alpha", 0);
        var teamB = new DraftTeam("Equipe Beta", 1);

        for (int i = 1; i <= 5; i++)
        {
            teamA.AddStarter(new DraftPlayer(Guid.NewGuid(), $"Alpha_{i}", $"7656119800000000{i}"));
            teamB.AddStarter(new DraftPlayer(Guid.NewGuid(), $"Beta_{i}", $"7656119800000001{i}"));
        }

        // Equipe Beta na Radiant, Equipe Alpha na Dire
        var radiant = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto
        {
            SteamId = 76561198000000010L + i,
            Nome = $"Beta_{i}"
        }).ToList();

        var dire = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto
        {
            SteamId = 76561198000000000L + i,
            Nome = $"Alpha_{i}"
        }).ToList();

        // Não deve lançar exceção
        validator.ValidateRostersMatch(radiant, dire, teamA, teamB);
    }

    [Fact]
    public void ReplayValidationService_ValidateRostersMatch_WhenIntruderOnDire_ShouldThrowDescriptiveDomainException()
    {
        var validator = new ReplayValidationService();
        var teamA = new DraftTeam("Equipe Alpha", 0);
        var teamB = new DraftTeam("Equipe Beta", 1);

        for (int i = 1; i <= 5; i++)
        {
            teamA.AddStarter(new DraftPlayer(Guid.NewGuid(), $"Alpha_{i}", $"7656119800000000{i}"));
            teamB.AddStarter(new DraftPlayer(Guid.NewGuid(), $"Beta_{i}", $"7656119800000001{i}"));
        }

        var radiant = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto
        {
            SteamId = 76561198000000000L + i,
            Nome = $"Alpha_{i}"
        }).ToList();

        var dire = new List<ParseGoDotaPlayerDto>
        {
            new() { SteamId = 76561198000000011L, Nome = "Beta_1" },
            new() { SteamId = 76561198000000012L, Nome = "Beta_2" },
            new() { SteamId = 76561198000000013L, Nome = "Beta_3" },
            new() { SteamId = 76561198000000014L, Nome = "Beta_4" },
            new() { SteamId = 99911122233344455L, Nome = "IntrusoDire" }
        };

        var ex = Assert.Throws<DomainException>(() =>
            validator.ValidateRostersMatch(radiant, dire, teamA, teamB));

        Assert.Contains("não corresponde aos times inscritos", ex.Message);
        Assert.Contains("99911122233344455", ex.Message);
        Assert.Contains("IntrusoDire", ex.Message);
        Assert.Contains("Equipe Beta", ex.Message);
    }

    [Fact]
    public void ReplayRosterMatcherService_ResolveIsTeamARadiant_WhenRosterDoesNotMatch_ShouldThrowDomainException()
    {
        var matcher = new ReplayRosterMatcherService();
        var teamAPlayers = Enumerable.Range(1, 5).Select(i => new DraftPlayer(Guid.NewGuid(), $"A_{i}", $"7656119800000000{i}")).ToList();
        var teamBPlayers = Enumerable.Range(1, 5).Select(i => new DraftPlayer(Guid.NewGuid(), $"B_{i}", $"7656119800000001{i}")).ToList();

        var radiant = Enumerable.Range(1, 4).Select(i => new ParseGoDotaPlayerDto { SteamId = 76561198000000000L + i }).ToList();
        radiant.Add(new ParseGoDotaPlayerDto { SteamId = 99999999999999999L });
        var dire = Enumerable.Range(1, 5).Select(i => new ParseGoDotaPlayerDto { SteamId = 76561198000000010L + i }).ToList();

        var ex = Assert.Throws<DomainException>(() =>
            matcher.ResolveIsTeamARadiant(radiant, dire, teamAPlayers, teamBPlayers));

        Assert.Contains("não corresponde aos times inscritos", ex.Message);
    }

    [Fact]
    public void ReplayRosterMatcherService_MapSidePlayers_WhenPlayerNotFoundInRoster_ShouldThrowDomainException()
    {
        var matcher = new ReplayRosterMatcherService();
        var teamId = Guid.NewGuid();
        var roster = new List<DraftPlayer>
        {
            new(Guid.NewGuid(), "LegitPlayer", "76561198000000001")
        };

        var sidePlayers = new List<ParseGoDotaPlayerDto>
        {
            new() { SteamId = 88888888888888888L, Nome = "UnknownPlayer" }
        };

        var ex = Assert.Throws<DomainException>(() =>
            matcher.MapSidePlayers(sidePlayers, roster, teamId));

        Assert.Contains("não pertence ao elenco", ex.Message);
        Assert.Contains("88888888888888888", ex.Message);
    }
}

