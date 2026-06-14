using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Application.Ports;

public interface IPricingClient
{
    Task<ShippingPrice> GetPriceAsync(
        ShippingMode mode,
        RouteOption route,
        PackageData package,
        CancellationToken cancellationToken);
}

public sealed record ShippingPrice(
    decimal Cost,
    decimal? Discount
);
