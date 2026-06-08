using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application.Ports;

public interface IRoutingClient
{
    Task<IReadOnlyList<RouteOption>> GetRoutesAsync(
        Guid originFulfillmentCenterId,
        AddressDto destination,
        PackageData package,
        CancellationToken cancellationToken);
}

public sealed record RouteOption(
    string Carrier,
    int TransitDays,
    bool Available,
    int Priority
);
