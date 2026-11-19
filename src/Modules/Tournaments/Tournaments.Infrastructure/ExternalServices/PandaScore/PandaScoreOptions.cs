namespace Tournaments.Infrastructure.ExternalServices.PandaScore;

public class PandaScoreOptions
{
    public const string SectionName = "PandaScore";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.pandascore.co";
    public int LiveCacheSeconds { get; set; } = 120;
    public int UpcomingCacheSeconds { get; set; } = 300;
    public int TournamentsCacheSeconds { get; set; } = 600;
    public int UpcomingWindowDays { get; set; } = 7;
}
