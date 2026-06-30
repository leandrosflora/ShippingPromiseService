using ShippingPromiseService.Application;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.UnitTests.Application;

public sealed class CacheKeyFactoryTests
{
    [Fact]
    public void Build_SameRequest_ProducesSameKey()
    {
        var request = MakeRequest();

        var key1 = CacheKeyFactory.Build(request);
        var key2 = CacheKeyFactory.Build(request);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void Build_KeyStartsWithPromisePrefix()
    {
        var key = CacheKeyFactory.Build(MakeRequest());

        Assert.StartsWith("promise:", key);
    }

    [Fact]
    public void Build_DifferentSeller_ProducesDifferentKey()
    {
        var skuId = Guid.NewGuid();
        var request1 = MakeRequest(sellerId: Guid.NewGuid(), skuId: skuId);
        var request2 = MakeRequest(sellerId: Guid.NewGuid(), skuId: skuId);

        Assert.NotEqual(CacheKeyFactory.Build(request1), CacheKeyFactory.Build(request2));
    }

    [Fact]
    public void Build_DifferentZipCode_ProducesDifferentKey()
    {
        var seller = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var request1 = MakeRequest(sellerId: seller, skuId: skuId, zipCode: "01310-100");
        var request2 = MakeRequest(sellerId: seller, skuId: skuId, zipCode: "20040-020");

        Assert.NotEqual(CacheKeyFactory.Build(request1), CacheKeyFactory.Build(request2));
    }

    [Fact]
    public void Build_DifferentState_ProducesDifferentKey()
    {
        var seller = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var request1 = MakeRequest(sellerId: seller, skuId: skuId, state: "SP");
        var request2 = MakeRequest(sellerId: seller, skuId: skuId, state: "RJ");

        Assert.NotEqual(CacheKeyFactory.Build(request1), CacheKeyFactory.Build(request2));
    }

    [Fact]
    public void Build_DifferentCountry_ProducesDifferentKey()
    {
        var seller = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var request1 = MakeRequest(sellerId: seller, skuId: skuId, country: "BR");
        var request2 = MakeRequest(sellerId: seller, skuId: skuId, country: "AR");

        Assert.NotEqual(CacheKeyFactory.Build(request1), CacheKeyFactory.Build(request2));
    }

    [Fact]
    public void Build_DifferentSkuId_ProducesDifferentKey()
    {
        var seller = Guid.NewGuid();
        var request1 = MakeRequest(sellerId: seller, skuId: Guid.NewGuid());
        var request2 = MakeRequest(sellerId: seller, skuId: Guid.NewGuid());

        Assert.NotEqual(CacheKeyFactory.Build(request1), CacheKeyFactory.Build(request2));
    }

    [Fact]
    public void Build_ItemOrderDoesNotAffectKey()
    {
        var seller = Guid.NewGuid();
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();

        var request1 = new ShippingPromiseRequest(
            CheckoutId: null,
            BuyerId: Guid.NewGuid(),
            SellerId: seller,
            Destination: new AddressDto("01310-100", "São Paulo", "SP", "BR"),
            Items: [
                new ShippingPromiseItemDto(sku1, 1, 10m),
                new ShippingPromiseItemDto(sku2, 2, 20m)
            ]
        );
        var request2 = new ShippingPromiseRequest(
            CheckoutId: null,
            BuyerId: Guid.NewGuid(),
            SellerId: seller,
            Destination: new AddressDto("01310-100", "São Paulo", "SP", "BR"),
            Items: [
                new ShippingPromiseItemDto(sku2, 2, 20m),
                new ShippingPromiseItemDto(sku1, 1, 10m)
            ]
        );

        Assert.Equal(CacheKeyFactory.Build(request1), CacheKeyFactory.Build(request2));
    }

    [Fact]
    public void Build_DifferentQuantity_ProducesDifferentKey()
    {
        var seller = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var request1 = new ShippingPromiseRequest(null, Guid.NewGuid(), seller,
            new AddressDto("01310-100", "SP", "SP", "BR"),
            [new ShippingPromiseItemDto(skuId, 1, 10m)]);
        var request2 = new ShippingPromiseRequest(null, Guid.NewGuid(), seller,
            new AddressDto("01310-100", "SP", "SP", "BR"),
            [new ShippingPromiseItemDto(skuId, 3, 10m)]);

        Assert.NotEqual(CacheKeyFactory.Build(request1), CacheKeyFactory.Build(request2));
    }

    [Fact]
    public void Build_BuyerIdDoesNotAffectKey()
    {
        var seller = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var request1 = MakeRequest(sellerId: seller, skuId: skuId) with { BuyerId = Guid.NewGuid() };
        var request2 = MakeRequest(sellerId: seller, skuId: skuId) with { BuyerId = Guid.NewGuid() };

        Assert.Equal(CacheKeyFactory.Build(request1), CacheKeyFactory.Build(request2));
    }

    private static ShippingPromiseRequest MakeRequest(
        Guid? sellerId = null,
        Guid? skuId = null,
        string zipCode = "01310-100",
        string state = "SP",
        string country = "BR") =>
        new(
            CheckoutId: null,
            BuyerId: Guid.NewGuid(),
            SellerId: sellerId ?? Guid.NewGuid(),
            Destination: new AddressDto(zipCode, "São Paulo", state, country),
            Items: [new ShippingPromiseItemDto(skuId ?? Guid.NewGuid(), 1, 50m)]
        );
}
