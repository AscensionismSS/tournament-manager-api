using System.Net;
using System.Text;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace TournamentCreator.WebApi.Services;

public static class ProductionSecurityValidator
{
    public const string RateLimitPolicyAuthStrict = "AuthStrict";
    public const string RateLimitPolicySteamPreview = "SteamPreview";

    private static readonly string[] DisallowedJwtSecrets =
    [
        "CHANGE_ME_IN_DEVELOPMENT_OR_ENV_MIN_32_CHARS_LONG!",
        "TournamentCreatorSecretKeySuperSecureMinimum256BitsLong2026!"
    ];

    public static void ValidateJwtSecret(string? secretKey, bool isDevelopment)
    {
        var trimmed = secretKey?.Trim();

        if (isDevelopment)
        {
            if (!string.IsNullOrWhiteSpace(trimmed) &&
                (trimmed.Length < 32 || Encoding.UTF8.GetByteCount(trimmed) < 32))
            {
                throw new InvalidOperationException(
                    "Segurança Crítica: 'Jwt:SecretKey' deve possuir no mínimo 32 caracteres (256 bits) para assinatura HMAC-SHA256.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(trimmed) ||
            trimmed.Length < 32 ||
            Encoding.UTF8.GetByteCount(trimmed) < 32 ||
            trimmed.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
            DisallowedJwtSecrets.Any(d => string.Equals(trimmed, d, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Segurança Crítica: 'Jwt:SecretKey' deve possuir no mínimo 32 caracteres e não pode usar valores padrão de desenvolvimento em Produção. Defina a variável de ambiente 'Jwt__SecretKey'.");
        }
    }

    public const string DefaultProductionFrontendOrigin = "https://tsurus-cup.pages.dev";

    public static void ValidateAdminSeedPassword(string? adminPassword, bool isDevelopment)
    {
        if (isDevelopment)
            return;

        var trimmed = adminPassword?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) ||
            trimmed.Length < 8 ||
            string.Equals(trimmed, "Admin123!", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Segurança Crítica: Para realizar o seed inicial de SuperAdmin em Produção, defina explicitamente 'AdminSeed__Password' com uma senha forte (mínimo 8 caracteres e diferente de 'Admin123!').");
        }
    }

    /// <summary>
    /// Resolve as origens permitidas para CORS.
    /// Retorna <c>null</c> apenas em Development quando configurado com '*' ou vazio (permitindo portas locais dinâmicas do Flutter Web).
    /// Em Produção (!isDevelopment), bloqueia estritamente wildcard '*' com credenciais, normalizando barras finais.
    /// </summary>
    public static string[]? ResolveAllowedCorsOrigins(string? rawAllowedOrigins, bool isDevelopment)
    {
        var parts = (rawAllowedOrigins ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (isDevelopment && (parts.Length == 0 || parts.Contains("*", StringComparer.Ordinal)))
        {
            return null;
        }

        var sanitized = parts
            .Where(o => !string.Equals(o, "*", StringComparison.Ordinal))
            .Select(o => o.TrimEnd('/'))
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sanitized.Length == 0)
        {
            return isDevelopment ? null : [DefaultProductionFrontendOrigin];
        }

        return sanitized;
    }

    /// <summary>
    /// Configura a política de CORS respeitando isolamento estrito em Produção e portas dinâmicas de loopback em Development.
    /// Em Produção (!isDevelopment), aplica exclusivamente <see cref="CorsPolicyBuilder.WithOrigins"/> sem wildcards nem loopback implícito.
    /// Em Development, permite origens loopback (<c>localhost</c>, <c>127.0.0.1</c>, <c>[::1]</c> em qualquer porta) mesmo quando
    /// <c>appsettings.Development.json</c> não existir e a configuração herdar <c>Cors:AllowedOrigins</c> do <c>appsettings.json</c>.
    /// </summary>
    public static void ConfigureCorsPolicy(CorsPolicyBuilder policy, string? rawAllowedOrigins, bool isDevelopment)
    {
        var allowedOrigins = ResolveAllowedCorsOrigins(rawAllowedOrigins, isDevelopment);

        if (!isDevelopment)
        {
            policy.WithOrigins(allowedOrigins ?? [DefaultProductionFrontendOrigin])
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
            return;
        }

        if (allowedOrigins is null)
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
            return;
        }

        var configuredSet = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);
        policy.SetIsOriginAllowed(origin => IsDevelopmentOriginAllowed(origin, configuredSet))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    }

    public static bool IsDevelopmentOriginAllowed(string? origin, IReadOnlySet<string>? configuredOrigins)
    {
        if (IsLoopbackOrigin(origin))
            return true;

        if (string.IsNullOrWhiteSpace(origin) || configuredOrigins is null || configuredOrigins.Count == 0)
            return false;

        return configuredOrigins.Contains(origin.Trim().TrimEnd('/'));
    }

    public static bool IsLoopbackOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (uri.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6))
            return false;

        if (!IPAddress.TryParse(uri.DnsSafeHost, out var ip))
            return false;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        return IPAddress.IsLoopback(ip);
    }
}
