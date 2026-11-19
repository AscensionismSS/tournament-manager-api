using Tournaments.Contracts.ProMatches;

namespace Tournaments.Infrastructure.ExternalServices.PandaScore;

public static class PandaScoreProMatchesMapper
{
    public static ProMatchDto MapMatch(PandaScoreMatch match, bool isLive)
    {
        var opponentA = match.Opponents?.ElementAtOrDefault(0)?.Opponent;
        var opponentB = match.Opponents?.ElementAtOrDefault(1)?.Opponent;

        var teamA = new ProTeamDto(
            TeamId: opponentA?.Id,
            Name: string.IsNullOrWhiteSpace(opponentA?.Name) ? "TBD" : opponentA.Name.Trim(),
            Acronym: string.IsNullOrWhiteSpace(opponentA?.Acronym) ? null : opponentA.Acronym.Trim(),
            LogoUrl: SanitizeHttpUrl(opponentA?.ImageUrl)
        );

        var teamB = new ProTeamDto(
            TeamId: opponentB?.Id,
            Name: string.IsNullOrWhiteSpace(opponentB?.Name) ? "TBD" : opponentB.Name.Trim(),
            Acronym: string.IsNullOrWhiteSpace(opponentB?.Acronym) ? null : opponentB.Acronym.Trim(),
            LogoUrl: SanitizeHttpUrl(opponentB?.ImageUrl)
        );

        var leagueName = string.IsNullOrWhiteSpace(match.League?.Name) ? "Dota 2 Pro" : match.League.Name.Trim();
        var tournamentName = BuildTournamentDisplayName(match.League?.Name, match.Serie?.FullName, match.Tournament?.Name);

        return new ProMatchDto(
            MatchId: match.Id,
            TournamentId: match.Tournament?.Id ?? match.TournamentId,
            TournamentName: tournamentName,
            LeagueName: leagueName,
            LeagueLogoUrl: SanitizeHttpUrl(match.League?.ImageUrl),
            Status: isLive ? "Live" : "Upcoming",
            Format: FormatMatchSeries(match.MatchType, match.NumberOfGames),
            ScheduledAtUtc: match.BeginAt ?? match.ScheduledAt,
            TeamA: teamA,
            TeamB: teamB,
            ScoreTeamA: ResolveTeamScore(match.Results, opponentA?.Id),
            ScoreTeamB: ResolveTeamScore(match.Results, opponentB?.Id),
            Streams: ExtractSupportedStreams(match.StreamsList)
        );
    }

    public static IReadOnlyList<ProTournamentGroupDto> BuildTournamentGroups(
        IReadOnlyList<PandaScoreTournament> runningTournaments,
        IReadOnlyList<ProMatchDto> liveMatches,
        IReadOnlyList<ProMatchDto> upcomingMatches)
    {
        var tournamentMap = new Dictionary<long, TournamentAccumulator>();

        foreach (var t in runningTournaments)
        {
            if (t.Id <= 0)
                continue;

            tournamentMap[t.Id] = new TournamentAccumulator
            {
                TournamentId = t.Id,
                Name = BuildTournamentDisplayName(t.League?.Name, t.Serie?.FullName, t.Name),
                LeagueName = t.League?.Name ?? t.Name ?? "Dota 2 Pro",
                LeagueLogoUrl = SanitizeHttpUrl(t.League?.ImageUrl),
                Tier = NormalizeTier(t.Tier),
                PrizePool = string.IsNullOrWhiteSpace(t.Prizepool) ? null : t.Prizepool.Trim(),
                BeginAtUtc = t.BeginAt,
                EndAtUtc = t.EndAt
            };
        }

        foreach (var match in liveMatches)
            GetOrCreateAccumulator(tournamentMap, match).LiveMatches.Add(match);

        foreach (var match in upcomingMatches)
            GetOrCreateAccumulator(tournamentMap, match).UpcomingMatches.Add(match);

        return tournamentMap.Values
            .Select(acc => new ProTournamentGroupDto(
                TournamentId: acc.TournamentId,
                Name: acc.Name,
                LeagueName: acc.LeagueName,
                LeagueLogoUrl: acc.LeagueLogoUrl,
                Tier: acc.Tier,
                PrizePool: acc.PrizePool,
                BeginAtUtc: acc.BeginAtUtc,
                EndAtUtc: acc.EndAtUtc,
                HasLiveMatches: acc.LiveMatches.Count > 0,
                LiveMatches: acc.LiveMatches,
                UpcomingMatches: acc.UpcomingMatches
            ))
            .OrderByDescending(t => t.HasLiveMatches)
            .ThenBy(t => GetTierRank(t.Tier))
            .ThenBy(t => t.LiveMatches.FirstOrDefault()?.ScheduledAtUtc
                      ?? t.UpcomingMatches.FirstOrDefault()?.ScheduledAtUtc
                      ?? t.BeginAtUtc
                      ?? DateTime.MaxValue)
            .ToList();
    }

    public static IReadOnlyList<ProStreamLinkDto> ExtractSupportedStreams(IReadOnlyList<PandaScoreStream>? rawStreams)
    {
        if (rawStreams is null || rawStreams.Count == 0)
            return [];

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ProStreamLinkDto>();

        foreach (var stream in rawStreams)
        {
            var url = SanitizeHttpUrl(stream.RawUrl);
            var normalizedLang = NormalizeSupportedLanguage(stream.Language);
            if (url is null || normalizedLang is null || !seenUrls.Add(url))
                continue;

            result.Add(new ProStreamLinkDto(
                Platform: DetectPlatform(url),
                Language: normalizedLang,
                LanguageLabel: normalizedLang == "pt" ? "PT-BR" : "EN",
                IsOfficial: stream.Official,
                IsMain: stream.Main,
                Url: url
            ));
        }

        return result
            .OrderBy(s => s.Language == "pt" ? 0 : 1)
            .ThenByDescending(s => s.IsMain)
            .ThenByDescending(s => s.IsOfficial)
            .ThenBy(s => GetPlatformOrder(s.Platform))
            .ToList();
    }

    private static TournamentAccumulator GetOrCreateAccumulator(
        Dictionary<long, TournamentAccumulator> map,
        ProMatchDto match)
    {
        if (!map.TryGetValue(match.TournamentId, out var acc))
        {
            acc = new TournamentAccumulator
            {
                TournamentId = match.TournamentId,
                Name = match.TournamentName,
                LeagueName = match.LeagueName,
                LeagueLogoUrl = match.LeagueLogoUrl
            };
            map[match.TournamentId] = acc;
        }

        return acc;
    }

    private static string? NormalizeSupportedLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var normalized = language.Trim().ToLowerInvariant();
        if (normalized == "pt" || normalized.StartsWith("pt-", StringComparison.Ordinal))
            return "pt";

        if (normalized == "en" || normalized.StartsWith("en-", StringComparison.Ordinal))
            return "en";

        return null;
    }

    private static string DetectPlatform(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "Other";

        var host = uri.Host.ToLowerInvariant();
        if (host == "twitch.tv" || host.EndsWith(".twitch.tv", StringComparison.Ordinal))
            return "Twitch";

        if (host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal) || host == "youtu.be")
            return "YouTube";

        if (host == "kick.com" || host.EndsWith(".kick.com", StringComparison.Ordinal))
            return "Kick";

        return "Other";
    }

    private static int GetPlatformOrder(string platform) => platform switch
    {
        "Twitch" => 0,
        "YouTube" => 1,
        "Kick" => 2,
        _ => 3
    };

    private static string? SanitizeHttpUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    private static int ResolveTeamScore(IReadOnlyList<PandaScoreMatchResult>? results, long? teamId) =>
        results is null || !teamId.HasValue
            ? 0
            : results.FirstOrDefault(r => r.TeamId == teamId.Value)?.Score ?? 0;

    private static string FormatMatchSeries(string? matchType, int numberOfGames) =>
        numberOfGames > 0
            ? $"BO{numberOfGames}"
            : !string.IsNullOrWhiteSpace(matchType) ? matchType.Trim().ToUpperInvariant() : "BO3";

    private static string BuildTournamentDisplayName(string? leagueName, string? serieFullName, string? tournamentName)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(leagueName))
            parts.Add(leagueName.Trim());

        if (!string.IsNullOrWhiteSpace(serieFullName) &&
            !parts.Any(p => p.Contains(serieFullName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add(serieFullName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(tournamentName) &&
            !parts.Any(p => p.Contains(tournamentName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add(tournamentName.Trim());
        }

        return parts.Count > 0 ? string.Join(" — ", parts) : "Campeonato Pro Dota 2";
    }

    private static string? NormalizeTier(string? tier) =>
        string.IsNullOrWhiteSpace(tier) ? null : tier.Trim().ToUpperInvariant();

    private static int GetTierRank(string? tier) => tier?.ToUpperInvariant() switch
    {
        "S" => 0,
        "A" => 1,
        "B" => 2,
        "C" => 3,
        "D" => 4,
        _ => 5
    };

    private sealed class TournamentAccumulator
    {
        public long TournamentId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string LeagueName { get; init; } = string.Empty;
        public string? LeagueLogoUrl { get; init; }
        public string? Tier { get; init; }
        public string? PrizePool { get; init; }
        public DateTime? BeginAtUtc { get; init; }
        public DateTime? EndAtUtc { get; init; }
        public List<ProMatchDto> LiveMatches { get; } = [];
        public List<ProMatchDto> UpcomingMatches { get; } = [];
    }
}
