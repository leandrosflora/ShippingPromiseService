using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Application.Ports;

public interface IShippingPromiseAuditRepository
{
    Task SaveAsync(
        ShippingPromiseRequest request,
        ShippingPromiseResponse response,
        IReadOnlyList<DeliveryCandidate> candidates,
        CancellationToken cancellationToken);
}
