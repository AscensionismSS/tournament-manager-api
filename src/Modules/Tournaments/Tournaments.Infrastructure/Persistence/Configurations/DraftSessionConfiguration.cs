using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tournaments.Domain.Entities;

namespace Tournaments.Infrastructure.Persistence.Configurations;

public class DraftSessionConfiguration : IEntityTypeConfiguration<DraftSession>
{
    public void Configure(EntityTypeBuilder<DraftSession> builder)
    {
        builder.ToTable("DraftSessions");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DraftVersion)
            .IsConcurrencyToken();

        builder.Property(d => d.TournamentId)
            .IsRequired();

        builder.HasIndex(d => d.TournamentId);

        builder.Property(d => d.State)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(d => d.PreviousState)
            .HasConversion<string>()
            .IsRequired(false);

        builder.Property(d => d.TargetTeamCount)
            .IsRequired();

        builder.Property(d => d.CurrentPickIndex)
            .IsRequired();

        builder.OwnsMany(d => d.PlayerPool, b =>
        {
            b.ToJson();
        });
        builder.Navigation(d => d.PlayerPool)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(d => d.Teams)
            .WithOne()
            .HasForeignKey("DraftSessionId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Teams)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
