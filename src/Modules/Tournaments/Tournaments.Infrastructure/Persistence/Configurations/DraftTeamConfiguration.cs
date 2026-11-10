using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tournaments.Domain.Entities;

namespace Tournaments.Infrastructure.Persistence.Configurations;

public class DraftTeamConfiguration : IEntityTypeConfiguration<DraftTeam>
{
    public void Configure(EntityTypeBuilder<DraftTeam> builder)
    {
        builder.ToTable("DraftTeams");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.CaptainId)
            .IsRequired(false);

        builder.Property(t => t.PickOrder)
            .IsRequired();

        builder.HasIndex("DraftSessionId");

        builder.OwnsMany(t => t.Starters, b =>
        {
            b.ToJson();
        });
        builder.Navigation(t => t.Starters)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(t => t.Reserves, b =>
        {
            b.ToJson();
        });
        builder.Navigation(t => t.Reserves)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
