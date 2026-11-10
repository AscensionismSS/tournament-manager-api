using Identity.Domain.Enums;

namespace TournamentCreator.WebApi.Controllers.v1.Requests;

public record LoginApiRequest(string? Email, string Password, string? Login = null, string? Identifier = null)
{
    public string GetEffectiveIdentifier() =>
        !string.IsNullOrWhiteSpace(Identifier) ? Identifier
        : !string.IsNullOrWhiteSpace(Login) ? Login
        : Email ?? string.Empty;
}

public record RegisterWithInviteApiRequest(string Code, string Password, string DisplayName, string? Email = null, ulong? SteamId = null);

public record LinkSteamApiRequest(ulong SteamId);

public record UpdateRolesApiRequest(IReadOnlyList<DotaRole> Roles, DotaRole? PreferredRole = null);

public record CreateInvitationApiRequest(string Email, UserRole TargetRole, int? ValidHours = 24);

public record ForgotPasswordApiRequest(string Email);

public record ResetPasswordApiRequest(string Token, string NewPassword, string ConfirmPassword);

public record ForgotLoginApiRequest(string Email);

public record RequestSteamUnlockApiRequest(string? Reason = null);
