namespace Tournaments.Domain.Entities;

public record PlayerGameStats(
    Guid PlayerId,
    Guid TeamId,
    int HeroId,
    int Kills,
    int Deaths,
    int Assists,
    int NetWorth,
    int GoldPerMinute,
    int ExperiencePerMinute,
    IReadOnlyList<int>? ItemIds,
    string? PlayerName = null,
    string? SteamName = null,
    string? HeroName = null,
    IReadOnlyList<string>? ItemNames = null,
    IReadOnlyList<string>? BackpackNames = null,
    string? NeutralItem = null,
    IReadOnlyList<string>? NeutralItems = null,
    string? JungleBuff = null,
    IReadOnlyList<string>? JungleBuffs = null,
    bool HasAghanimScepter = false,
    bool HasAghanimShard = false,
    int WardsPlaced = 0,
    int WardsDestroyed = 0,
    long TowerDamage = 0,
    long HeroDamage = 0,
    long HeroHealing = 0
)
{
    private PlayerGameStats() : this(Guid.Empty, Guid.Empty, 0, 0, 0, 0, 0, 0, 0, null, null, null, null, null, null, null, null, null, null, false, false, 0, 0, 0, 0, 0) { }
}
