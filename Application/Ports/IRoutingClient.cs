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
    string RouteId,
    string OriginNodeId,
    string? DestinationNodeId,
    string CarrierCode,
    string ServiceLevelCode,
    int TransitDays,
    bool Available,
    int Priority
);
