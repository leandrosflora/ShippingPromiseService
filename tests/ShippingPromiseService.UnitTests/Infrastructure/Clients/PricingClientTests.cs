using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;
using ShippingPromiseService.Infrastructure.Clients;

namespace ShippingPromiseService.UnitTests.Infrastructure.Clients;

public sealed class PricingClientTests
{
    [Fact]
    public async Task GetPriceAsync_WhenLogisticsCostIsNull_ReturnsCustomerPrice()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler("""
            {
              "quotes": [
                {
                  "customerPrice": 12.34,
                  "logisticsCost": null,
                  "discount": 1.23
                }
              ]
            }
            """))
        {
            BaseAddress = new Uri("https://pricing.test")
        };
        var client = new PricingClient(httpClient, NullLogger<PricingClient>.Instance);

        var price = await client.GetPriceAsync(
            MakeRequest(),
            ShippingMode.Fulfillment,
            new RouteOption("route-1", "origin-1", "destination-1", "carrier-1", "standard", 2, true, 1),
            new PackageData(1m, 2m, 10m, 20m, 30m, false, false),
            "standard",
            CancellationToken.None);

        Assert.Equal(12.34m, price.Cost);
        Assert.Equal(1.23m, price.Discount);
    }

    private static ShippingPromiseRequest MakeRequest() => new(
        CheckoutId: Guid.NewGuid(),
        BuyerId: Guid.NewGuid(),
        SellerId: Guid.NewGuid(),
        Destination: new AddressDto("01001000", "São Paulo", "SP", "BR"),
        Items: [new ShippingPromiseItemDto(Guid.NewGuid(), 1, 100m)]);

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };

            return Task.FromResult(response);
        }
    }
}
