using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application.Ports;

public interface IShippingPromiseCache
{
    Task<ShippingPromiseResponse?> GetAsync(
        string key,
        CancellationToken cancellationToken);

    Task SetAsync(
        string key,
        ShippingPromiseResponse response,
        TimeSpan ttl,
        CancellationToken cancellationToken);
}
