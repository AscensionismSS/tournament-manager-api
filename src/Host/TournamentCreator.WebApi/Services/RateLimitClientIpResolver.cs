using System.Net;
using System.Net.Sockets;

namespace TournamentCreator.WebApi.Services;

public static class RateLimitClientIpResolver
{
    public static string ResolveClientIp(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp is not null && remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        // Confia em X-Forwarded-For apenas quando a conexão TCP imediata vem de proxy reverso interno (loopback/rede privada ex: Render/Docker/Cloudflare)
        if (remoteIp is null || IsPrivateOrLoopback(remoteIp))
        {
            var xff = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(xff))
            {
                var parts = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                string? rightmostValidIp = null;

                // Algoritmo Rightmost-Non-Private: caminha da direita para a esquerda ignorando saltos internos de proxy privado,
                // suportando formato IP puro e IP:porta (ex: IIS/ARR/Envoy/Azure) e normalizando sub-redes IPv6 /64.
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    if (TryParseForwardedIp(parts[i], out var parsedForwardedIp))
                    {
                        var partitionKey = FormatPartitionKey(parsedForwardedIp);
                        rightmostValidIp ??= partitionKey;

                        if (!IsPrivateOrLoopback(parsedForwardedIp))
                        {
                            return partitionKey;
                        }
                    }
                }

                if (rightmostValidIp is not null)
                {
                    return rightmostValidIp;
                }
            }
        }

        return remoteIp is not null ? FormatPartitionKey(remoteIp) : "unknown";
    }

    public static bool TryParseForwardedIp(string rawToken, out IPAddress ip)
    {
        ip = IPAddress.None;
        if (string.IsNullOrWhiteSpace(rawToken))
            return false;

        var token = rawToken.Trim().Trim('"');
        if (IPAddress.TryParse(token, out var directIp))
        {
            ip = directIp.IsIPv4MappedToIPv6 ? directIp.MapToIPv4() : directIp;
            return true;
        }

        if (IPEndPoint.TryParse(token, out var endPoint))
        {
            ip = endPoint.Address.IsIPv4MappedToIPv6 ? endPoint.Address.MapToIPv4() : endPoint.Address;
            return true;
        }

        return false;
    }

    public static string FormatPartitionKey(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        // Agrupa endereços IPv6 públicos por prefixo /64 para evitar bypass trivial por rotação de interface SLAAC/privacy
        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !IsPrivateOrLoopback(ip))
        {
            Span<byte> bytes = stackalloc byte[16];
            ip.TryWriteBytes(bytes, out _);
            bytes[8..16].Clear();
            return $"{new IPAddress(bytes)}/64";
        }

        return ip.ToString();
    }

    public static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10)
                return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            // 100.64.0.0/10 (CGNAT usado em proxies internos de PaaS como Render/Fly)
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                return true;
            // 169.254.0.0/16
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;

            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                return true;

            byte[] bytes = ip.GetAddressBytes();
            // fc00::/7 (Unique Local Address)
            return (bytes[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}
