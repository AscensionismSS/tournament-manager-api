using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Identity.Application.Common.Interfaces;
using Identity.Contracts.Steam;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.ExternalServices;

public class OpenDotaProfileService : IOpenDotaProfileService
{
    private const ulong SteamIdOffset = 76561197960265728ul;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenDotaProfileService> _logger;

    public OpenDotaProfileService(HttpClient httpClient, ILogger<OpenDotaProfileService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DotaProfileLookupResult?> FetchProfileAsync(ulong steamId, CancellationToken ct = default)
    {
        try
        {
            if (steamId < SteamIdOffset)
            {
                _logger.LogWarning("SteamID64 {SteamId} inválido para cálculo de AccountId.", steamId);
                return null;
            }

            ulong accountId = steamId - SteamIdOffset;
            string url = $"https://api.opendota.com/api/players/{accountId}";

            var response = await _httpClient.GetFromJsonAsync<OpenDotaPlayerResponse>(url, ct);
            if (response?.Profile is null)
            {
                _logger.LogInformation("OpenDota não retornou perfil público para SteamId {SteamId}.", steamId);
                return new DotaProfileLookupResult(
                    steamId,
                    $"Player_{accountId}",
                    null,
                    response?.RankTier,
                    response?.LeaderboardRank
                );
            }

            return new DotaProfileLookupResult(
                steamId,
                response.Profile.Personaname ?? $"Player_{accountId}",
                response.Profile.Avatarfull,
                response.RankTier,
                response.LeaderboardRank
            );
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

        // 1. Se já for numérico puro
        if (ulong.TryParse(input, out ulong directId))
        {
            if (directId >= SteamIdOffset)
                return directId;

            // Se for número de Friend ID / Account ID (ex: 72668302)
            if (directId > 0 && directId < 1_000_000_000_000ul)
                return SteamIdOffset + directId;
        }

        // 2. Se for URL padrão com /profiles/{17_digitos}
        var profileMatch = System.Text.RegularExpressions.Regex.Match(input, @"profiles\/(\d{17})");
        if (profileMatch.Success && ulong.TryParse(profileMatch.Groups[1].Value, out ulong urlId))
        {
            return urlId;
        }

        // 3. Se for URL com /id/ (Custom Vanity URL) ou vanity name
        string? vanityName = null;
        if (input.Contains("/id/"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(input, @"\/id\/([^\/?#]+)");
            if (match.Success)
            {
                vanityName = match.Groups[1].Value;
            }
        }
        else if (!input.Contains('/') && !input.Contains('.') && !input.Contains(':'))
        {
            vanityName = input;
        }

        if (!string.IsNullOrWhiteSpace(vanityName))
        {
            try
            {
                string xmlUrl = $"https://steamcommunity.com/id/{vanityName}/?xml=1";
                string xml = await _httpClient.GetStringAsync(xmlUrl, ct);
                var xmlMatch = System.Text.RegularExpressions.Regex.Match(xml, @"<steamID64>(\d{17})<\/steamID64>");
                if (xmlMatch.Success && ulong.TryParse(xmlMatch.Groups[1].Value, out ulong resolvedId))
                {
                    _logger.LogInformation("Vanity URL '{VanityName}' resolvida com sucesso para SteamID64 {SteamId}.", vanityName, resolvedId);
                    return resolvedId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao resolver Vanity URL da Steam para '{VanityName}'.", vanityName);
            }
        }

        // 4. Fallback: qualquer sequência de 17 dígitos no texto
        var fallbackMatch = System.Text.RegularExpressions.Regex.Match(input, @"\b(7656119\d{10})\b");
        if (fallbackMatch.Success && ulong.TryParse(fallbackMatch.Groups[1].Value, out ulong digitsId))
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
