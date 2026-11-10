using BuildingBlocks.Domain;

namespace Identity.Domain.Entities;

public record UserPreferences
{
    public static UserPreferences Default => new("Player");

    public string PreferredLandingPage { get; init; } = "Player";

    public UserPreferences() { }

    public UserPreferences(string preferredLandingPage)
    {
        if (string.IsNullOrWhiteSpace(preferredLandingPage))
            throw new DomainException("A página inicial preferencial é obrigatória.");

        var normalized = preferredLandingPage.Trim();
        if (!normalized.Equals("Player", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("A página inicial preferencial deve ser 'Player' ou 'Admin'.");
        }

        PreferredLandingPage = normalized.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Player";
    }
}
