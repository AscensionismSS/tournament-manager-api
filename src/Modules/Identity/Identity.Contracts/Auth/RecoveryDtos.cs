namespace Identity.Contracts.Auth;

public record ForgotPasswordRequestDto(string Email);

public record ResetPasswordRequestDto(string Token, string NewPassword, string ConfirmPassword);

public record ForgotLoginRequestDto(string Email);

public record RecoveryActionResponseDto(string Message);

public record RecoveredLoginDto(string Login, string Email);

public record RecoveryRequestAdminDto(
    Guid Id,
    Guid UserId,
    string Email,
    string? DisplayName,
    string Token,
    string Type,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsUsed,
    DateTime? UsedAtUtc,
    string ActionUrl,
    string? AvatarUrl = null,
    string? SteamId = null,
    string? PersonaName = null
);
