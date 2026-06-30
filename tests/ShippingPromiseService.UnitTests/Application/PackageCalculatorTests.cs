using ShippingPromiseService.Application;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.UnitTests.Application;

public sealed class PackageCalculatorTests
{
    private readonly PackageCalculator _calculator = new();

    [Fact]
    public void Calculate_SingleItem_ComputesWeightAndDimensions()
    {
        var skuId = Guid.NewGuid();
        var request = MakeRequest([new ShippingPromiseItemDto(skuId, Quantity: 2, UnitPrice: 100m)]);
        var products = new List<ProductPhysicalInfo>
        {
            new(skuId, WeightKg: 1.5m, HeightCm: 10m, WidthCm: 20m, LengthCm: 30m, Category: "electronics", IsFragile: false, IsRestricted: false)
        };

        var package = _calculator.Calculate(request, products);

        Assert.Equal(3.0m, package.TotalWeightKg);   // 1.5 * 2
        Assert.Equal(10m, package.HeightCm);
        Assert.Equal(20m, package.WidthCm);
        Assert.Equal(60m, package.LengthCm);          // 30 * 2
        Assert.Equal(2m, package.CubicWeightKg);      // (10 * 20 * 60) / 6000 = 2
        Assert.False(package.HasFragileItem);
        Assert.False(package.HasRestrictedItem);
    }

    [Fact]
    public void Calculate_MultipleItems_UsesMaxHeightAndWidth_AccumulatesLengthAndWeight()
    {
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var request = MakeRequest([
            new ShippingPromiseItemDto(sku1, Quantity: 1, UnitPrice: 10m),
            new ShippingPromiseItemDto(sku2, Quantity: 1, UnitPrice: 20m)
        ]);
        var products = new List<ProductPhysicalInfo>
        {
            new(sku1, WeightKg: 1.0m, HeightCm: 5m, WidthCm: 10m, LengthCm: 15m, Category: "A", IsFragile: false, IsRestricted: false),
            new(sku2, WeightKg: 2.0m, HeightCm: 8m, WidthCm: 7m, LengthCm: 20m, Category: "B", IsFragile: false, IsRestricted: false)
        };

        var package = _calculator.Calculate(request, products);

        Assert.Equal(3.0m, package.TotalWeightKg);  // 1.0 + 2.0
        Assert.Equal(8m, package.HeightCm);          // max(5, 8)
        Assert.Equal(10m, package.WidthCm);          // max(10, 7)
        Assert.Equal(35m, package.LengthCm);         // 15 + 20
    }

    [Fact]
    public void Calculate_QuantityMultipliesWeightAndLength()
    {
        var skuId = Guid.NewGuid();
        var request = MakeRequest([new ShippingPromiseItemDto(skuId, Quantity: 3, UnitPrice: 10m)]);
        var products = new List<ProductPhysicalInfo>
        {
            new(skuId, WeightKg: 2m, HeightCm: 5m, WidthCm: 5m, LengthCm: 10m, Category: "test", IsFragile: false, IsRestricted: false)
        };

        var package = _calculator.Calculate(request, products);

        Assert.Equal(6m, package.TotalWeightKg);  // 2 * 3
        Assert.Equal(30m, package.LengthCm);      // 10 * 3
        Assert.Equal(5m, package.HeightCm);       // unchanged (max of a single product)
    }

    [Fact]
    public void Calculate_WithFragileProduct_SetsHasFragileItem()
    {
        var skuId = Guid.NewGuid();
        var request = MakeRequest([new ShippingPromiseItemDto(skuId, 1, 10m)]);
        var products = new List<ProductPhysicalInfo>
        {
            new(skuId, 1m, 10m, 10m, 10m, "glass", IsFragile: true, IsRestricted: false)
        };

        var package = _calculator.Calculate(request, products);

        Assert.True(package.HasFragileItem);
        Assert.False(package.HasRestrictedItem);
    }

    [Fact]
    public void Calculate_WithRestrictedProduct_SetsHasRestrictedItem()
    {
        var skuId = Guid.NewGuid();
        var request = MakeRequest([new ShippingPromiseItemDto(skuId, 1, 10m)]);
        var products = new List<ProductPhysicalInfo>
        {
            new(skuId, 1m, 10m, 10m, 10m, "chemicals", IsFragile: false, IsRestricted: true)
        };

        var package = _calculator.Calculate(request, products);

        Assert.True(package.HasRestrictedItem);
        Assert.False(package.HasFragileItem);
    }

    [Fact]
    public void Calculate_CubicWeightUsesMaxHeightAndMaxWidthAndTotalLength()
    {
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var request = MakeRequest([
            new ShippingPromiseItemDto(sku1, Quantity: 1, UnitPrice: 10m),
            new ShippingPromiseItemDto(sku2, Quantity: 2, UnitPrice: 20m)
        ]);
        var products = new List<ProductPhysicalInfo>
        {
            new(sku1, WeightKg: 1m, HeightCm: 10m, WidthCm: 20m, LengthCm: 30m, Category: "A", IsFragile: false, IsRestricted: false),
            new(sku2, WeightKg: 1m, HeightCm: 5m, WidthCm: 15m, LengthCm: 20m, Category: "B", IsFragile: false, IsRestricted: false)
        };

        // maxH=10, maxW=20, totalL=30+(20*2)=70
        // cubicWeight = (10 * 20 * 70) / 6000 = 14000 / 6000 ≈ 2.3333...
        var package = _calculator.Calculate(request, products);

        var expected = (10m * 20m * 70m) / 6000m;
        Assert.Equal(expected, package.CubicWeightKg);
    }

    private static ShippingPromiseRequest MakeRequest(IReadOnlyList<ShippingPromiseItemDto> items) =>
        new(
            CheckoutId: null,
            BuyerId: Guid.NewGuid(),
            SellerId: Guid.NewGuid(),
            Destination: new AddressDto("01310-100", "São Paulo", "SP", "BR"),
            Items: items
        );
}
