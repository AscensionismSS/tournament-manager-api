using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Code)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(i => i.Code)
            .IsUnique();

        builder.Property(i => i.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(i => i.Email);

        builder.Property(i => i.TargetRole)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.CreatedByUserId)
            .IsRequired();

        builder.Property(i => i.CreatedAtUtc)
            .IsRequired();

        builder.Property(i => i.ExpiresAtUtc)
            .IsRequired();

        builder.Property(i => i.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(i => i.RevokedAtUtc);

        builder.Ignore(i => i.ValidHours);
    }
}
