using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class AccountRecoveryTokenConfiguration : IEntityTypeConfiguration<AccountRecoveryToken>
{
    public void Configure(EntityTypeBuilder<AccountRecoveryToken> builder)
    {
        builder.ToTable("AccountRecoveryTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId)
            .IsRequired();

        builder.Property(t => t.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(t => t.Token)
            .IsUnique();

        builder.Property(t => t.Type)
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.ExpiresAtUtc)
            .IsRequired();

        builder.Property(t => t.IsUsed)
            .IsRequired();

        builder.Property(t => t.UsedAtUtc)
            .IsRequired(false);
    }
}
