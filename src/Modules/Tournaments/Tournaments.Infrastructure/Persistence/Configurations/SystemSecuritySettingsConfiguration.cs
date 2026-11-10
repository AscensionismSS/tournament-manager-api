using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tournaments.Domain.Entities;

namespace Tournaments.Infrastructure.Persistence.Configurations;

public class SystemSecuritySettingsConfiguration : IEntityTypeConfiguration<SystemSecuritySettings>
{
    public void Configure(EntityTypeBuilder<SystemSecuritySettings> builder)
    {
        builder.ToTable("SystemSecuritySettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.LockResultSubmission)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.LockTournamentRegistration)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.LockTournamentCreation)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.LockDraftPicks)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.MaintenanceMode)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.Message)
            .HasMaxLength(1000);

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(150);

        builder.Property(s => s.UpdatedAtUtc);
    }
}
