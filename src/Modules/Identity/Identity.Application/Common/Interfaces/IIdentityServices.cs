using Identity.Domain.Entities;

namespace Identity.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string passwordHash);
}

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}

public interface IOpenDotaProfileService
{
    Task<Identity.Contracts.Steam.DotaProfileLookupResult?> FetchProfileAsync(ulong steamId, CancellationToken ct = default);
    Task<ulong> ResolveSteamIdAsync(string identifierOrUrl, CancellationToken ct = default);
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
    bool IsTournamentAdmin { get; }
}
