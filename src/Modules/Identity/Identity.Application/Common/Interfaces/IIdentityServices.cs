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

public record UserAuthSnapshot(bool Exists, bool IsActive, string Role);

public interface IUserAuthStateCache
{
    Task<UserAuthSnapshot> GetOrLoadAsync(
        Guid userId,
        Func<CancellationToken, Task<UserAuthSnapshot>> factory,
        CancellationToken ct = default);

    void Invalidate(Guid userId);
}

public interface IRootOwnerService
{
    string RootOwnerEmail { get; }
    bool IsRootOwnerEmail(string? email);
    bool IsRootOwner(User? user);
    bool HasRootOwnerAuthority(User? user);
}
