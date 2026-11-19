using System.Collections.Concurrent;
using Identity.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Identity.Infrastructure.Security;

public sealed class MemoryUserAuthStateCache : IUserAuthStateCache
{
    public static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _memoryCache;
    private readonly TimeSpan _cacheDuration;
    private readonly ConcurrentDictionary<Guid, long> _userEpochs = new();

    public MemoryUserAuthStateCache(IMemoryCache memoryCache, TimeSpan? cacheDuration = null)
    {
        _memoryCache = memoryCache;
        _cacheDuration = cacheDuration ?? DefaultCacheDuration;
    }

    public static string GetCacheKey(Guid userId) => $"identity:user-auth-state:{userId:D}";

    public async Task<UserAuthSnapshot> GetOrLoadAsync(
        Guid userId,
        Func<CancellationToken, Task<UserAuthSnapshot>> factory,
        CancellationToken ct = default)
    {
        var key = GetCacheKey(userId);
        if (_memoryCache.TryGetValue(key, out UserAuthSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        long epochBefore = GetEpoch(userId);
        var loaded = await factory(ct);

        // Protege contra race condition de invalidação concorrente:
        // se Invalidate(userId) rodou enquanto factory(ct) lia o estado antigo do banco,
        // descarta o snapshot obsoleto e recarrega o estado atualizado sem envenenar o cache.
        if (GetEpoch(userId) != epochBefore)
        {
            epochBefore = GetEpoch(userId);
            loaded = await factory(ct);
            if (GetEpoch(userId) != epochBefore)
            {
                return loaded;
            }
        }

        _memoryCache.Set(key, loaded, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheDuration
        });

        return loaded;
    }

    public void Invalidate(Guid userId)
    {
        _userEpochs.AddOrUpdate(userId, 1L, static (_, current) => current + 1L);
        _memoryCache.Remove(GetCacheKey(userId));
    }

    private long GetEpoch(Guid userId) => _userEpochs.TryGetValue(userId, out var epoch) ? epoch : 0L;
}
