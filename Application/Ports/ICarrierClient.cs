using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application.Ports;

public interface ICarrierClient
{
    Task<bool> IsCarrierAvailableAsync(
        string carrier,
        AddressDto destination,
        CancellationToken cancellationToken);
}
