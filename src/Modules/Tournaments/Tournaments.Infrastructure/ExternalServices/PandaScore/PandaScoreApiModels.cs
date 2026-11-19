using System.Text.Json.Serialization;

namespace Tournaments.Infrastructure.ExternalServices.PandaScore;

public sealed class PandaScoreMatch
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tournament_id")]
    public long TournamentId { get; set; }

    [JsonPropertyName("match_type")]
    public string? MatchType { get; set; }

    [JsonPropertyName("number_of_games")]
    public int NumberOfGames { get; set; }

    [JsonPropertyName("begin_at")]
    public DateTime? BeginAt { get; set; }

    [JsonPropertyName("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    [JsonPropertyName("league")]
    public PandaScoreLeague? League { get; set; }

    [JsonPropertyName("serie")]
    public PandaScoreSerie? Serie { get; set; }

    [JsonPropertyName("tournament")]
    public PandaScoreTournamentRef? Tournament { get; set; }

    [JsonPropertyName("opponents")]
    public List<PandaScoreOpponentWrapper>? Opponents { get; set; }

    [JsonPropertyName("results")]
    public List<PandaScoreMatchResult>? Results { get; set; }

    [JsonPropertyName("streams_list")]
    public List<PandaScoreStream>? StreamsList { get; set; }
}

public sealed class PandaScoreTournament
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("prizepool")]
    public string? Prizepool { get; set; }

    [JsonPropertyName("begin_at")]
    public DateTime? BeginAt { get; set; }

    [JsonPropertyName("end_at")]
    public DateTime? EndAt { get; set; }

    [JsonPropertyName("league")]
    public PandaScoreLeague? League { get; set; }

    [JsonPropertyName("serie")]
    public PandaScoreSerie? Serie { get; set; }
}

public sealed class PandaScoreTournamentRef
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("prizepool")]
    public string? Prizepool { get; set; }

    [JsonPropertyName("begin_at")]
    public DateTime? BeginAt { get; set; }

    [JsonPropertyName("end_at")]
    public DateTime? EndAt { get; set; }
}

public sealed class PandaScoreLeague
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
}

public sealed class PandaScoreSerie
{
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }
}

public sealed class PandaScoreOpponentWrapper
{
    [JsonPropertyName("opponent")]
    public PandaScoreTeam? Opponent { get; set; }
}

public sealed class PandaScoreTeam
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("acronym")]
    public string? Acronym { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
}

public sealed class PandaScoreMatchResult
{
    [JsonPropertyName("team_id")]
    public long TeamId { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

public sealed class PandaScoreStream
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("main")]
    public bool Main { get; set; }

    [JsonPropertyName("official")]
    public bool Official { get; set; }

    [JsonPropertyName("raw_url")]
    public string? RawUrl { get; set; }
}
