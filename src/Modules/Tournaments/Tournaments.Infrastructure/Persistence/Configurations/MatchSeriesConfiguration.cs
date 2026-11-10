using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tournaments.Domain.Entities;

namespace Tournaments.Infrastructure.Persistence.Configurations;

public class MatchSeriesConfiguration : IEntityTypeConfiguration<MatchSeries>
{
    public void Configure(EntityTypeBuilder<MatchSeries> builder)
    {
        builder.ToTable("MatchSeries");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TournamentId)
            .IsRequired();

        builder.HasIndex(s => s.TournamentId);
        builder.HasIndex(s => new { s.TournamentId, s.SlotCode });

        builder.Property(s => s.SlotCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Round)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(s => s.Format)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.TeamAName)
            .HasMaxLength(150);

        builder.Property(s => s.TeamBName)
            .HasMaxLength(150);

        builder.HasMany(s => s.Games)
            .WithOne()
            .HasForeignKey(g => g.MatchSeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Games)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
