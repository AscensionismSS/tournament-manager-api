namespace Identity.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "TournamentCreatorSecretKeySuperSecureMinimum256BitsLong2026!";
    public string Issuer { get; set; } = "TournamentCreator";
    public string Audience { get; set; } = "TournamentCreatorApp";
    public int ExpiryMinutes { get; set; } = 480; // 8 hours
}
