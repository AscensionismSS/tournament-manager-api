using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tournaments.Domain.Entities;

namespace Tournaments.Infrastructure.Persistence.Configurations;

public class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable("Tournaments");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.ComplexProperty(t => t.FormatConfig, b =>
        {
            b.Property(f => f.Type).HasConversion<string>().HasColumnName("FormatType");
            b.Property(f => f.PlayoffFormat).HasConversion<string>().HasColumnName("FormatPlayoff");
            b.Property(f => f.TeamCount).HasColumnName("TeamCount");
            b.Property(f => f.GroupCount).HasColumnName("GroupCount");
        });

        builder.ComplexProperty(t => t.SeriesConfig, b =>
        {
            b.Property(s => s.GroupStageFormat).HasConversion<string>().HasColumnName("GroupStageFormat");
            b.Property(s => s.PlayoffFormat).HasConversion<string>().HasColumnName("PlayoffSeriesFormat");
            b.Property(s => s.GrandFinalFormat).HasConversion<string>().HasColumnName("GrandFinalSeriesFormat");
        });

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.LastUpdatedAtUtc)
            .IsRequired();

        builder.Property(t => t.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(t => t.BannerUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(t => t.ChampionId)
            .IsRequired(false);

        builder.Property(t => t.ChampionName)
            .HasMaxLength(150)
            .IsRequired(false);

        builder.Property(t => t.IsResultSubmissionLocked)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.ResultLockReason)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(t => t.ResultLockedAtUtc)
            .IsRequired(false);

        builder.Property(t => t.ResultLockedBy)
            .HasMaxLength(150)
            .IsRequired(false);

        builder.OwnsMany(t => t.Teams, b =>
        {
            b.ToJson();
        });
        builder.Navigation(t => t.Teams)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(t => t.BracketSlots, b =>
        {
            b.ToJson();
        });
        builder.Navigation(t => t.BracketSlots)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
