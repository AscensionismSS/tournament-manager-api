namespace Identity.Contracts.Preferences;

public record GeneralPreferencesDto(
    string PreferredLandingPage
);

public record UpdateGeneralPreferencesDto(
    string PreferredLandingPage
);
