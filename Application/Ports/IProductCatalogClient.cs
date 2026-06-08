namespace ShippingPromiseService.Application.Ports;

public interface IProductCatalogClient
{
    Task<IReadOnlyList<ProductPhysicalInfo>> GetProductsAsync(
        IReadOnlyList<Guid> skuIds,
        CancellationToken cancellationToken);
}

public sealed record ProductPhysicalInfo(
    Guid SkuId,
    decimal WeightKg,
    decimal HeightCm,
    decimal WidthCm,
    decimal LengthCm,
    string Category,
    bool IsFragile,
    bool IsRestricted
);
