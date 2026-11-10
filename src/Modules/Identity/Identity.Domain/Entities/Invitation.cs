using BuildingBlocks.Domain;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public class Invitation : AggregateRoot<Guid>
{
    public static readonly IReadOnlyList<int> AllowedExpirationHours = new[] { 24, 48, 72 };

    public string Code { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public UserRole TargetRole { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public Guid? UsedByUserId { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsUsed => UsedAtUtc.HasValue;
    public bool IsExpired => DateTime.UtcNow > ExpiresAtUtc;
    public int ValidHours => (int)Math.Max(1, Math.Round((ExpiresAtUtc - CreatedAtUtc).TotalHours));

    protected Invitation() { }

    public Invitation(
        string email,
        UserRole targetRole,
        Guid createdByUserId,
        TimeSpan? validFor = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("O email de convite é obrigatório.");

        if (validFor.HasValue)
        {
            int requestedHours = (int)Math.Round(validFor.Value.TotalHours);
            if (!AllowedExpirationHours.Contains(requestedHours))
            {
                throw new DomainException($"A validade do convite deve ser de 24, 48 ou 72 horas. Informado: {requestedHours}h.");
            }
        }

        Id = Guid.NewGuid();
        Code = GenerateSecureCode();
        Email = email.Trim().ToLowerInvariant();
        TargetRole = targetRole;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.Add(validFor ?? TimeSpan.FromHours(24));
        IsRevoked = false;
    }

    public void Accept(Guid registeredUserId)
    {
        if (IsRevoked)
            throw new DomainException("Este convite foi desativado pelo administrador.");

        if (IsUsed)
            throw new DomainException("Este convite já foi utilizado.");

        if (IsExpired)
            throw new DomainException("Este convite está expirado.");

        UsedAtUtc = DateTime.UtcNow;
        UsedByUserId = registeredUserId;
    }

    public void Revoke()
    {
        if (IsUsed)
            throw new DomainException("Não é possível desativar um convite que já foi utilizado.");

        if (IsRevoked)
            throw new DomainException("Este convite já está desativado.");

        IsRevoked = true;
        RevokedAtUtc = DateTime.UtcNow;
    }

    private static string GenerateSecureCode()
    {
        // 32-character hexadecimal token (16 cryptographically random bytes)
        byte[] bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
