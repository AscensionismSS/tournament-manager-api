namespace Identity.Infrastructure.Security;

public sealed class RootOwnerOptions
{
    public const string SectionName = "AdminSeed";
    public const string DefaultEmail = "admin@tournament.local";

    public string Email { get; set; } = DefaultEmail;
}
