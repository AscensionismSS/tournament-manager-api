using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tournaments.Domain.Entities;

namespace Tournaments.Infrastructure.Persistence.Configurations;

public class MatchGameConfiguration : IEntityTypeConfiguration<MatchGame>
{
    public void Configure(EntityTypeBuilder<MatchGame> builder)
    {
        builder.ToTable("MatchGames");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.MatchSeriesId)
            .IsRequired();

        builder.HasIndex(g => g.MatchSeriesId);
        builder.HasIndex(g => new { g.MatchSeriesId, g.GameNumber }).IsUnique();

        builder.Property(g => g.GameNumber)
            .IsRequired();

        builder.Property(g => g.ResultSource)
            .HasConversion<string>()
            .IsRequired();

        // Colunas relacionais normais para desempate e queries rápidas
        builder.Property(g => g.WinnerTeamId)
            .IsRequired();

        builder.Property(g => g.DurationSeconds)
            .IsRequired();

        builder.Property(g => g.TeamAKills)
            .IsRequired();

        builder.Property(g => g.TeamBKills)
            .IsRequired();

        builder.Property(g => g.IsWalkover)
            .IsRequired();

        builder.Property(g => g.DotaMatchId);

        builder.Property(g => g.FirstPickTeamId);

        builder.Property(g => g.TeamABanHeroIds)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) ? new List<int>() : System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<int>());

        builder.Property(g => g.TeamBBanHeroIds)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) ? new List<int>() : System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<int>());

        builder.Property(g => g.TeamARoshanKills)
            .HasDefaultValue(0);

        builder.Property(g => g.TeamBRoshanKills)
            .HasDefaultValue(0);

        builder.Property(g => g.TeamAAegisStolen)
            .HasDefaultValue(0);

        builder.Property(g => g.TeamBAegisStolen)
            .HasDefaultValue(0);

        // Mapeamento Híbrido: Estatísticas detalhadas de jogadores em JSONB
        builder.OwnsMany(g => g.PlayerStats, playerStatsBuilder =>
        {
            playerStatsBuilder.ToJson();
        });

        builder.Navigation(g => g.PlayerStats)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
