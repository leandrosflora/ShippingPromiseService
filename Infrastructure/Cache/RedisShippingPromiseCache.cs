using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Infrastructure.Cache;

public sealed class RedisShippingPromiseCache : IShippingPromiseCache
{
    private readonly IDistributedCache _cache;

    public RedisShippingPromiseCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<ShippingPromiseResponse?> GetAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(key, cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<ShippingPromiseResponse>(json);
    }

    public async Task SetAsync(
        string key,
        ShippingPromiseResponse response,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response);

        await _cache.SetStringAsync(
            key,
            json,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            },
            cancellationToken);
    }
}
