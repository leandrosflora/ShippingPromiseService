using ShippingPromiseService.Application;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.UnitTests.Application;

public sealed class FallbackEngineTests
{
    private readonly FallbackEngine _engine = new();

    [Fact]
    public void TryBuildFallback_WhenCountryIsNotBR_ReturnsNull()
    {
        var request = MakeRequest("US");

        var result = _engine.TryBuildFallback(request, new Exception("network error"));

        Assert.Null(result);
    }

    [Fact]
    public void TryBuildFallback_WhenCountryIsBR_ReturnsFallbackCandidate()
    {
        var request = MakeRequest("BR");

        var result = _engine.TryBuildFallback(request, new Exception("network error"));

        Assert.NotNull(result);
        Assert.Equal(ShippingMode.SellerShipping, result.Mode);
        Assert.Equal("DEFAULT_CARRIER", result.Carrier);
        Assert.Equal(29.90m, result.ShippingCost);
        Assert.Equal(999, result.Priority);
        Assert.True(result.IsFallback);
        Assert.Equal(Guid.Empty, result.OriginFulfillmentCenterId);
    }

    [Theory]
    [InlineData("br")]
    [InlineData("BR")]
    [InlineData("Br")]
    public void TryBuildFallback_CountryMatchIsCaseInsensitive(string country)
    {
        var request = MakeRequest(country);

        var result = _engine.TryBuildFallback(request, new Exception());

        Assert.NotNull(result);
    }

    [Fact]
    public void TryBuildFallback_WhenBR_EstimatedDeliveryDateIsSevenDaysFromNow()
    {
        var request = MakeRequest("BR");
        var expectedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var result = _engine.TryBuildFallback(request, new Exception());

        Assert.Equal(expectedDate, result!.EstimatedDeliveryDate);
    }

    [Theory]
    [InlineData("AR")]
    [InlineData("MX")]
    [InlineData("CO")]
    [InlineData("US")]
    public void TryBuildFallback_WhenCountryIsOtherThanBR_ReturnsNull(string country)
    {
        var request = MakeRequest(country);

        var result = _engine.TryBuildFallback(request, new TimeoutException());

        Assert.Null(result);
    }

    private static ShippingPromiseRequest MakeRequest(string country) =>
        new(
            CheckoutId: null,
            BuyerId: Guid.NewGuid(),
            SellerId: Guid.NewGuid(),
            Destination: new AddressDto("01310-100", "São Paulo", "SP", country),
            Items: [new ShippingPromiseItemDto(Guid.NewGuid(), 1, 50m)]
        );
}
