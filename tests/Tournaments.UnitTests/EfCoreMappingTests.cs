using Microsoft.EntityFrameworkCore;
using Tournaments.Domain.Entities;
using Tournaments.Infrastructure.Persistence;
using Xunit;

namespace Tournaments.UnitTests;

public class EfCoreMappingTests
{
    private TournamentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TournamentDbContext>()
            .UseNpgsql("Host=localhost;Database=TournamentEngineTest;Username=postgres;Password=postgres")
            .Options;

        return new TournamentDbContext(options);
    }

    [Fact]
    public void DraftSession_DraftVersion_ShouldBeConfiguredAsConcurrencyToken()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(DraftSession));
        Assert.NotNull(entityType);

        var versionProp = entityType.FindProperty(nameof(DraftSession.DraftVersion));
        Assert.NotNull(versionProp);
        Assert.True(versionProp.IsConcurrencyToken);
    }

    [Fact]
    public void MatchGame_ShouldHaveRelationalAndJsonStatsConfigured()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(MatchGame));
        Assert.NotNull(entityType);

        Assert.NotNull(entityType.FindProperty(nameof(MatchGame.WinnerTeamId)));
        Assert.NotNull(entityType.FindProperty(nameof(MatchGame.DurationSeconds)));
        Assert.NotNull(entityType.FindProperty(nameof(MatchGame.TeamAKills)));
        Assert.NotNull(entityType.FindProperty(nameof(MatchGame.TeamBKills)));
        Assert.NotNull(entityType.FindProperty(nameof(MatchGame.IsWalkover)));

        var playerStatsNavigation = entityType.FindNavigation(nameof(MatchGame.PlayerStats));
        Assert.NotNull(playerStatsNavigation);
    }

    [Fact]
    public void Tournament_ShouldHaveFormatAndSeriesComplexProperties()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Tournament));
        Assert.NotNull(entityType);

        var formatProp = entityType.FindComplexProperty(nameof(Tournament.FormatConfig));
        Assert.NotNull(formatProp);

        var seriesProp = entityType.FindComplexProperty(nameof(Tournament.SeriesConfig));
        Assert.NotNull(seriesProp);
    }

    [Fact]
    public void Tournament_Version_ShouldBeConfiguredAsConcurrencyToken()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Tournament));
        Assert.NotNull(entityType);

        var versionProp = entityType.FindProperty(nameof(Tournament.Version));
        Assert.NotNull(versionProp);
        Assert.True(versionProp.IsConcurrencyToken);
    }

    [Fact]
    public void MatchSeries_ShouldHaveIndexesConfigured()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(MatchSeries));
        Assert.NotNull(entityType);

        var indexes = entityType.GetIndexes().ToList();
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == nameof(MatchSeries.TournamentId)));
    }

    [Fact]
    public void MatchGame_ShouldHaveIndexesConfigured()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(MatchGame));
        Assert.NotNull(entityType);

        var indexes = entityType.GetIndexes().ToList();
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == nameof(MatchGame.MatchSeriesId)));
    }

    [Fact]
    public void DraftSession_ShouldHaveIndexesConfigured()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(DraftSession));
        Assert.NotNull(entityType);

        var indexes = entityType.GetIndexes().ToList();
        Assert.Contains(indexes, i => i.Properties.Any(p => p.Name == nameof(DraftSession.TournamentId)));
    }
}
