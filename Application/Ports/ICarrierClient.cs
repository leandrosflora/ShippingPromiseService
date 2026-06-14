using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application.Ports;

public interface ICarrierClient
{
    Task<bool> IsCarrierAvailableAsync(
        RouteOption route,
        AddressDto destination,
        PackageData package,
        CancellationToken cancellationToken);
}
