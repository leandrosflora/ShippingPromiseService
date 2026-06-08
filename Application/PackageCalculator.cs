using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application;

public sealed class PackageCalculator
{
    public PackageData Calculate(
        ShippingPromiseRequest request,
        IReadOnlyList<ProductPhysicalInfo> products)
    {
        decimal totalWeight = 0;
        decimal maxHeight = 0;
        decimal maxWidth = 0;
        decimal totalLength = 0;

        var hasFragile = false;
        var hasRestricted = false;

        foreach (var item in request.Items)
        {
            var product = products.Single(x => x.SkuId == item.SkuId);

            totalWeight += product.WeightKg * item.Quantity;
            maxHeight = Math.Max(maxHeight, product.HeightCm);
            maxWidth = Math.Max(maxWidth, product.WidthCm);
            totalLength += product.LengthCm * item.Quantity;

            hasFragile |= product.IsFragile;
            hasRestricted |= product.IsRestricted;
        }

        var cubicWeight = (maxHeight * maxWidth * totalLength) / 6000m;

        return new PackageData(
            TotalWeightKg: totalWeight,
            CubicWeightKg: cubicWeight,
            HeightCm: maxHeight,
            WidthCm: maxWidth,
            LengthCm: totalLength,
            HasFragileItem: hasFragile,
            HasRestrictedItem: hasRestricted
        );
    }
}
