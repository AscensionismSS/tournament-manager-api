using Identity.Application.Common.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

public sealed class RootOwnerService : IRootOwnerService
{
    public string RootOwnerEmail { get; }

    public RootOwnerService(IOptions<RootOwnerOptions> options)
        : this(options?.Value?.Email)
    {
    }

    public RootOwnerService(string? rootOwnerEmail)
    {
        RootOwnerEmail = string.IsNullOrWhiteSpace(rootOwnerEmail)
            ? RootOwnerOptions.DefaultEmail
            : rootOwnerEmail.Trim().ToLowerInvariant();
    }

    public bool IsRootOwnerEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return string.Equals(email.Trim(), RootOwnerEmail, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsRootOwner(User? user)
    {
        return user is not null && IsRootOwnerEmail(user.Email);
    }

    public bool HasRootOwnerAuthority(User? user)
    {
        return user is not null
            && user.IsActive
            && user.Role == UserRole.SuperAdmin
            && IsRootOwnerEmail(user.Email);
    }
}
