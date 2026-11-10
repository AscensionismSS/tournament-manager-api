namespace Identity.Contracts.Invitations;

public record InvitationDto(
    Guid Id,
    string Code,
    string Email,
    string TargetRole,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsUsed,
    DateTime? UsedAtUtc,
    bool IsRevoked = false,
    DateTime? RevokedAtUtc = null,
    int? ValidHours = null
);
