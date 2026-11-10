using System.Security.Cryptography;
using BuildingBlocks.Domain;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public class AccountRecoveryToken : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public RecoveryTokenType Type { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    protected AccountRecoveryToken() { }

    public AccountRecoveryToken(
        Guid userId,
        string email,
        RecoveryTokenType type,
        TimeSpan lifetime)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId é obrigatório.");

        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email é obrigatório.");

        Id = Guid.NewGuid();
        UserId = userId;
        Email = email.Trim().ToLowerInvariant();
        Type = type;
        Token = GenerateSecureToken();
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.Add(lifetime);
        IsUsed = false;
    }

    public bool IsActive(DateTime utcNow)
    {
        return !IsUsed && utcNow <= ExpiresAtUtc;
    }

    public void Consume(DateTime utcNow)
    {
        if (IsUsed)
            throw new DomainException("Este código de recuperação já foi utilizado.");

        if (utcNow > ExpiresAtUtc)
            throw new DomainException("Este código de recuperação está expirado.");

        IsUsed = true;
        UsedAtUtc = utcNow;
    }

    public void Reject(DateTime utcNow)
    {
        if (IsUsed)
            throw new DomainException("Esta solicitação já foi finalizada ou atendida anteriormente.");

        IsUsed = true;
        UsedAtUtc = utcNow;
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
