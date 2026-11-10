using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.SteamId);

        builder.HasIndex(u => u.SteamId)
            .IsUnique()
            .HasFilter("\"SteamId\" IS NOT NULL");

        builder.Property(u => u.IsSteamLocked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.DotaPersonaName)
            .HasMaxLength(128);

        builder.Property(u => u.DotaAvatarUrl)
            .HasMaxLength(512);

        var rolesComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyCollection<Identity.Domain.Enums.DotaRole>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => (IReadOnlyCollection<Identity.Domain.Enums.DotaRole>)c.ToList());

        builder.Property(u => u.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(
                v => string.Join(',', v.Select(r => r.ToString())),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<Identity.Domain.Enums.DotaRole>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(Enum.Parse<Identity.Domain.Enums.DotaRole>)
                       .ToList())
            .Metadata.SetValueComparer(rolesComparer);
        builder.Property(u => u.Roles)
            .HasMaxLength(256);

        builder.Property(u => u.PreferredRole)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.IsTournamentEligible)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.StatusReason)
            .HasMaxLength(500);

        builder.OwnsOne(u => u.Preferences, prefBuilder =>
        {
            prefBuilder.Property(p => p.PreferredLandingPage)
                .HasColumnName("PreferredLandingPage")
                .HasMaxLength(50)
                .HasDefaultValue("Player");
        });
    }
}
