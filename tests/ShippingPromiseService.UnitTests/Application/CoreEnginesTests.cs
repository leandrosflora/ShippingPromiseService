using Xunit;
using ShippingPromiseService.Application;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.UnitTests.Application;

public sealed class CoreEnginesTests
{
    [Fact]
    public void CacheKeyFactory_Build_IsStableForItemOrderingAndIncludesDestination()
    {
        var skuA = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var skuB = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var sellerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var first = new ShippingPromiseRequest(null, Guid.NewGuid(), sellerId, Address(), new[]
        {
            new ShippingPromiseItemDto(skuB, 2, 20m),
            new ShippingPromiseItemDto(skuA, 1, 10m)
        });
        var second = first with
        {
            Items = new[]
            {
                new ShippingPromiseItemDto(skuA, 1, 10m),
                new ShippingPromiseItemDto(skuB, 2, 20m)
            }
        };
        var otherDestination = first with { Destination = Address(zipCode: "20000-000") };

        Assert.Equal(CacheKeyFactory.Build(first), CacheKeyFactory.Build(second));
        Assert.NotEqual(CacheKeyFactory.Build(first), CacheKeyFactory.Build(otherDestination));
        Assert.StartsWith("promise:", CacheKeyFactory.Build(first));
    }

    [Fact]
    public void PackageCalculator_Calculate_AggregatesContractItemsWithoutExternalDependencies()
    {
        var skuA = Guid.NewGuid();
        var skuB = Guid.NewGuid();
        var request = new ShippingPromiseRequest(null, Guid.NewGuid(), Guid.NewGuid(), Address(), new[]
        {
            new ShippingPromiseItemDto(skuA, 2, 100m),
            new ShippingPromiseItemDto(skuB, 1, 50m)
        });
        var products = new[]
        {
            new ProductPhysicalInfo(skuA, 1.5m, 10m, 20m, 30m, "electronics", true, false),
            new ProductPhysicalInfo(skuB, 2.0m, 15m, 10m, 40m, "electronics", false, false)
        };

        var package = new PackageCalculator().Calculate(request, products);

        Assert.Equal(5.0m, package.TotalWeightKg);
        Assert.Equal(15m, package.HeightCm);
        Assert.Equal(20m, package.WidthCm);
        Assert.Equal(100m, package.LengthCm);
        Assert.Equal(5m, package.CubicWeightKg);
        Assert.True(package.HasFragileItem);
        Assert.False(package.HasRestrictedItem);
    }

    [Fact]
    public void DeliveryDecisionEngine_SelectBest_OrdersByDeliveryDateCostAndPriority()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3));
        var candidates = new[]
        {
            Candidate(date.AddDays(1), 1m, 1),
            Candidate(date, 20m, 1),
            Candidate(date, 10m, 5),
            Candidate(date, 10m, 2)
        };

        var selected = new DeliveryDecisionEngine().SelectBest(candidates);

        Assert.Equal(date, selected.EstimatedDeliveryDate);
        Assert.Equal(10m, selected.ShippingCost);
        Assert.Equal(2, selected.Priority);
    }

    [Fact]
    public void FallbackEngine_TryBuildFallback_ReturnsConservativeBrazilianPromiseOnly()
    {
        var fallback = new FallbackEngine().TryBuildFallback(
            Request(destination: Address(country: "BR")),
            new TimeoutException());
        var international = new FallbackEngine().TryBuildFallback(
            Request(destination: Address(country: "US")),
            new TimeoutException());

        Assert.NotNull(fallback);
        Assert.Equal(ShippingMode.SellerShipping, fallback.Mode);
        Assert.Equal("DEFAULT_CARRIER", fallback.Carrier);
        Assert.Equal(29.90m, fallback.ShippingCost);
        Assert.True(fallback.IsFallback);
        Assert.Null(international);
    }

    private static ShippingPromiseRequest Request(AddressDto? destination = null) =>
        new(null, Guid.NewGuid(), Guid.NewGuid(), destination ?? Address(), new[]
        {
            new ShippingPromiseItemDto(Guid.NewGuid(), 1, 99.90m)
        });

    private static AddressDto Address(
        string zipCode = "01310-100",
        string city = "São Paulo",
        string state = "SP",
        string country = "BR") => new(zipCode, city, state, country);

    private static DeliveryCandidate Candidate(DateOnly date, decimal cost, int priority) =>
        new(ShippingMode.Carrier, Guid.NewGuid(), "MELI", date, cost, priority, false);
}
