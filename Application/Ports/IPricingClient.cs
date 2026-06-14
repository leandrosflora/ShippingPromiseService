using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Application.Ports;

public interface IPricingClient
{
    Task<ShippingPrice> GetPriceAsync(
        ShippingPromiseRequest request,
        ShippingMode mode,
        RouteOption route,
        PackageData package,
        string packageCategory,
        CancellationToken cancellationToken);
}

public sealed record ShippingPrice(
    decimal Cost,
    decimal? Discount
);
