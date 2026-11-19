using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tournaments.Application.Common.Interfaces;
using Tournaments.Contracts.ProMatches;

namespace Tournaments.Infrastructure.ExternalServices.PandaScore;

public class PandaScoreProMatchesService : IProMatchesService
{
    private const string LiveCacheKey = "pro-matches:pandascore:live";
    private const string UpcomingCacheKey = "pro-matches:pandascore:upcoming";
    private const string TournamentsCacheKey = "pro-matches:pandascore:running-tournaments";

    private static readonly SemaphoreSlim LiveGate = new(1, 1);
    private static readonly SemaphoreSlim UpcomingGate = new(1, 1);
    private static readonly SemaphoreSlim TournamentsGate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly PandaScoreOptions _options;
    private readonly ILogger<PandaScoreProMatchesService> _logger;

    public PandaScoreProMatchesService(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IOptions<PandaScoreOptions> options,
        ILogger<PandaScoreProMatchesService> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProMatchesOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            return new ProMatchesOverviewDto(
                IsConfigured: false,
                FetchedAtUtc: DateTime.UtcNow,
                LiveMatches: [],
                Tournaments: []
            );
        }

        var liveTask = GetCachedListAsync<PandaScoreMatch>(
            LiveCacheKey,
            LiveGate,
            "/dota2/matches/running?per_page=30",
            TimeSpan.FromSeconds(Math.Clamp(_options.LiveCacheSeconds, 30, 600)),
            apiKey,
            cancellationToken);

        var upcomingTask = GetCachedListAsync<PandaScoreMatch>(
            UpcomingCacheKey,
            UpcomingGate,
            "/dota2/matches/upcoming?per_page=40&sort=begin_at",
            TimeSpan.FromSeconds(Math.Clamp(_options.UpcomingCacheSeconds, 60, 1800)),
            apiKey,
            cancellationToken);

        var tournamentsTask = GetCachedListAsync<PandaScoreTournament>(
            TournamentsCacheKey,
            TournamentsGate,
            "/dota2/tournaments/running?per_page=20",
            TimeSpan.FromSeconds(Math.Clamp(_options.TournamentsCacheSeconds, 120, 3600)),
            apiKey,
            cancellationToken);

        await Task.WhenAll(liveTask, upcomingTask, tournamentsTask);

        var rawLive = await liveTask;
        var rawUpcoming = await upcomingTask;
        var rawTournaments = await tournamentsTask;

        var nowUtc = DateTime.UtcNow;
        var maxUpcomingUtc = nowUtc.AddDays(Math.Clamp(_options.UpcomingWindowDays, 1, 30));
        var allowedTierLookup = PandaScoreProMatchesMapper.BuildAllowedTournamentTierLookup(rawTournaments);

        var liveMatches = rawLive
            .Select(m => PandaScoreProMatchesMapper.TryMapMatch(m, isLive: true, allowedTierLookup))
            .Where(m => m is not null)
            .Select(m => m!)
            .OrderBy(m => m.Tier == "S" ? 0 : 1)
            .ThenBy(m => m.ScheduledAtUtc ?? DateTime.MaxValue)
            .ToList();

        var upcomingMatches = rawUpcoming
            .Where(m =>
            {
                var start = m.BeginAt ?? m.ScheduledAt;
                return !start.HasValue || (start.Value >= nowUtc.AddHours(-2) && start.Value <= maxUpcomingUtc);
            })
            .Select(m => PandaScoreProMatchesMapper.TryMapMatch(m, isLive: false, allowedTierLookup))
            .Where(m => m is not null)
            .Select(m => m!)
            .OrderBy(m => m.Tier == "S" ? 0 : 1)
            .ThenBy(m => m.ScheduledAtUtc ?? DateTime.MaxValue)
            .ToList();

        var tournaments = PandaScoreProMatchesMapper.BuildTournamentGroups(rawTournaments, liveMatches, upcomingMatches);

        return new ProMatchesOverviewDto(
            IsConfigured: true,
            FetchedAtUtc: nowUtc,
            LiveMatches: liveMatches,
            Tournaments: tournaments
        );
    }

    private async Task<IReadOnlyList<T>> GetCachedListAsync<T>(
        string cacheKey,
        SemaphoreSlim gate,
        string relativePath,
        TimeSpan ttl,
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<T>? cached) && cached is not null)
        {
            return cached;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
                ? "https://api.pandascore.co"
                : _options.BaseUrl.TrimEnd('/');

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}{relativePath}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PandaScore retornou HTTP {StatusCode} ao consultar {Endpoint}.",
                    (int)response.StatusCode,
                    relativePath);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var parsed = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken) ?? [];

            _memoryCache.Set<IReadOnlyList<T>>(cacheKey, parsed, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            });

            return parsed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar PandaScore em {Endpoint}.", relativePath);
            return [];
        }
        finally
        {
            gate.Release();
        }
    }
}
