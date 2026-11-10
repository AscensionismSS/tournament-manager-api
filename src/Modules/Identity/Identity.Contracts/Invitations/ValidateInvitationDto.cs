namespace Identity.Contracts.Invitations;

public record ValidateInvitationDto(
    bool IsValid,
    string? Email = null,
    string? TargetRole = null,
    string? ErrorMessage = null
);
