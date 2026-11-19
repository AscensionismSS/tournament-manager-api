using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Steam;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.ExternalServices;

public partial class OpenDotaProfileService : IOpenDotaProfileService
{
    private const ulong SteamIdOffset = 76561197960265728ul;
    private const ulong MaxSteamId64 = SteamIdOffset + uint.MaxValue;
    private static readonly TimeSpan ProfileCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VanityResolutionCacheDuration = TimeSpan.FromMinutes(15);

    private static bool IsValidSteamId64(ulong steamId) =>
        steamId > SteamIdOffset && steamId <= MaxSteamId64;

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenDotaProfileService> _logger;
    private readonly IMemoryCache? _memoryCache;

    public OpenDotaProfileService(
        HttpClient httpClient,
        ILogger<OpenDotaProfileService> logger,
        IMemoryCache? memoryCache = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _memoryCache = memoryCache;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_-]{2,32}$", RegexOptions.CultureInvariant, 500)]
    private static partial Regex VanityNameRegex();

    [GeneratedRegex(@"^(?:(?:https?:\/\/)?(?:www\.)?steamcommunity\.com)?\/?profiles\/(\d{17})(?:[\/?#]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 500)]
    private static partial Regex ProfileIdRegex();

    [GeneratedRegex(@"^(?:(?:https?:\/\/)?(?:www\.)?steamcommunity\.com)?\/?id\/([^\/?#]+)(?:[\/?#]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 500)]
    private static partial Regex VanityUrlPathRegex();

    [GeneratedRegex(@"<steamID64>(\d{17})<\/steamID64>", RegexOptions.CultureInvariant, 500)]
    private static partial Regex SteamXmlIdRegex();

    [GeneratedRegex(@"\b(\d{17})\b", RegexOptions.CultureInvariant, 500)]
    private static partial Regex FallbackSteam64Regex();

    public async Task<DotaProfileLookupResult?> FetchProfileAsync(ulong steamId, CancellationToken ct = default)
    {
        try
        {
            if (!IsValidSteamId64(steamId))
            {
                _logger.LogWarning("SteamID64 {SteamId} inválido para cálculo de AccountId.", steamId);
                return null;
            }

            string cacheKey = $"opendota:profile:{steamId}";
            if (_memoryCache is not null && _memoryCache.TryGetValue(cacheKey, out DotaProfileLookupResult? cached) && cached is not null)
            {
                return cached;
            }

            ulong accountId = steamId - SteamIdOffset;
            string url = $"https://api.opendota.com/api/players/{accountId}";

            var response = await _httpClient.GetFromJsonAsync<OpenDotaPlayerResponse>(url, ct);
            DotaProfileLookupResult result;
            if (response?.Profile is null)
            {
                _logger.LogInformation("OpenDota não retornou perfil público para SteamId {SteamId}.", steamId);
                result = new DotaProfileLookupResult(
                    steamId,
                    $"Player_{accountId}",
                    null,
                    response?.RankTier,
                    response?.LeaderboardRank
                );
            }
            else
            {
                result = new DotaProfileLookupResult(
                    steamId,
                    response.Profile.Personaname ?? $"Player_{accountId}",
                    response.Profile.Avatarfull,
                    response.RankTier,
                    response.LeaderboardRank
                );
                _memoryCache?.Set(cacheKey, result, ProfileCacheDuration);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar perfil no OpenDota para SteamID {SteamId}.", steamId);
            return null;
        }
    }

    public async Task<ulong> ResolveSteamIdAsync(string identifierOrUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identifierOrUrl))
            return 0;

        string input = identifierOrUrl.Trim();

        // 1. Se já for numérico puro (SteamID64 individual válido ou AccountId/FriendId de 32 bits)
        if (ulong.TryParse(input, out ulong directId))
        {
            if (IsValidSteamId64(directId))
                return directId;

            if (directId > 0 && directId <= uint.MaxValue)
                return SteamIdOffset + directId;

            return 0;
        }

        // 2. Se for URL padrão com /profiles/{17_digitos}
        var profileMatch = ProfileIdRegex().Match(input);
        if (profileMatch.Success &&
            ulong.TryParse(profileMatch.Groups[1].Value, out ulong urlId) &&
            IsValidSteamId64(urlId))
        {
            return urlId;
        }

        // 3. Se for URL oficial da Steam com /id/ (Custom Vanity URL) ou vanity name puro
        string? vanityName = null;
        var vanityUrlMatch = VanityUrlPathRegex().Match(input);
        if (vanityUrlMatch.Success)
        {
            vanityName = vanityUrlMatch.Groups[1].Value;
        }
        else if (!input.Contains('/') && !input.Contains('\\') && !input.Contains('.') && !input.Contains(':'))
        {
            vanityName = input;
        }

        if (!string.IsNullOrWhiteSpace(vanityName))
        {
            if (VanityNameRegex().IsMatch(vanityName))
            {
                string vanityCacheKey = $"steam:vanity:{vanityName.ToLowerInvariant()}";
                if (_memoryCache is not null && _memoryCache.TryGetValue(vanityCacheKey, out ulong cachedSteamId) && cachedSteamId > 0)
                {
                    return cachedSteamId;
                }

                try
                {
                    string escapedVanity = Uri.EscapeDataString(vanityName);
                    string xmlUrl = $"https://steamcommunity.com/id/{escapedVanity}/?xml=1";
                    string xml = await _httpClient.GetStringAsync(xmlUrl, ct);
                    var xmlMatch = SteamXmlIdRegex().Match(xml);
                    if (xmlMatch.Success &&
                        ulong.TryParse(xmlMatch.Groups[1].Value, out ulong resolvedId) &&
                        IsValidSteamId64(resolvedId))
                    {
                        _memoryCache?.Set(vanityCacheKey, resolvedId, VanityResolutionCacheDuration);
                        _logger.LogInformation("Vanity URL '{VanityName}' resolvida com sucesso para SteamID64 {SteamId}.", vanityName, resolvedId);
                        return resolvedId;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao resolver Vanity URL da Steam para '{VanityName}'.", vanityName);
                }
            }
            else
            {
                _logger.LogWarning("Vanity name da Steam rejeitado por formato inválido: '{VanityName}'.", vanityName);
            }
        }

        // 4. Fallback: qualquer sequência válida de 17 dígitos no texto
        var fallbackMatch = FallbackSteam64Regex().Match(input);
        if (fallbackMatch.Success &&
            ulong.TryParse(fallbackMatch.Groups[1].Value, out ulong digitsId) &&
            IsValidSteamId64(digitsId))
        {
            return digitsId;
        }

        return 0;
    }

    private sealed class OpenDotaPlayerResponse
    {
        [JsonPropertyName("profile")]
        public OpenDotaProfile? Profile { get; set; }

        [JsonPropertyName("rank_tier")]
        public int? RankTier { get; set; }

        [JsonPropertyName("leaderboard_rank")]
        public int? LeaderboardRank { get; set; }
    }

    private sealed class OpenDotaProfile
    {
        [JsonPropertyName("account_id")]
        public long AccountId { get; set; }

        [JsonPropertyName("personaname")]
        public string? Personaname { get; set; }

        [JsonPropertyName("avatarfull")]
        public string? Avatarfull { get; set; }
    }
}
