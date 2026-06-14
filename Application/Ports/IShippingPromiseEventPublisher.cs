using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application.Ports;

public interface IShippingPromiseEventPublisher
{
    Task PublishCalculatedAsync(
        ShippingPromiseRequest request,
        ShippingPromiseResponse response,
        string correlationId,
        CancellationToken cancellationToken);
}
