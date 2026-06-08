namespace ShippingPromiseService.Application.Ports;

public interface IInventoryClient
{
    Task<IReadOnlyList<InventoryAvailability>> GetAvailabilityAsync(
        Guid sellerId,
        IReadOnlyList<Guid> skuIds,
        CancellationToken cancellationToken);
}

public sealed record InventoryAvailability(
    Guid SkuId,
    Guid FulfillmentCenterId,
    int AvailableQuantity
);
