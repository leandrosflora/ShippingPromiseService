using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Application.Ports;

public interface IShippingPromiseAuditRepository
{
    Task<ShippingPromiseResponse?> GetByPromiseIdAsync(
        string promiseId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        ShippingPromiseRequest request,
        ShippingPromiseResponse response,
        IReadOnlyList<DeliveryCandidate> candidates,
        CancellationToken cancellationToken);
}
