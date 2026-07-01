using System.Net.Http.Json;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Infrastructure.Clients;

public sealed class PricingClient : IPricingClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PricingClient> _logger;

    public PricingClient(HttpClient httpClient, ILogger<PricingClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ShippingPrice> GetPriceAsync(
        ShippingPromiseRequest request,
        ShippingMode mode,
        RouteOption route,
        PackageData package,
        string packageCategory,
        CancellationToken cancellationToken)
    {
        var candidateId = string.IsNullOrWhiteSpace(route.RouteId)
            ? $"{route.OriginNodeId}:{route.CarrierCode}:{route.ServiceLevelCode}"
            : route.RouteId;

        var pricingRequest = new
        {
            buyerId = request.BuyerId,
            sellerId = request.SellerId,
            destinationPostalCode = request.Destination.ZipCode,
            cartTotal = request.Items.Sum(item => item.UnitPrice * item.Quantity),
            currency = "BRL",
            requestedAtUtc = DateTimeOffset.UtcNow,
            candidates = new[]
            {
                new
                {
                    candidateId,
                    routeId = route.RouteId,
                    originNodeId = route.OriginNodeId,
                    carrierCode = route.CarrierCode,
                    serviceLevelCode = route.ServiceLevelCode,
                    package = new
                    {
                        actualWeightKg = package.TotalWeightKg,
                        cubicWeightKg = package.CubicWeightKg,
                        isFragile = package.HasFragileItem,
                        isRestricted = package.HasRestrictedItem,
                        category = packageCategory
                    }
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/shipping-prices/quotes/batch",
            pricingRequest,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Pricing service failed with status {StatusCode}", response.StatusCode);
            return new ShippingPrice(Cost: 0, Discount: null);
        }

        var batch = await response.Content.ReadFromJsonAsync<PricingBatchResponse>(cancellationToken);
        var price = batch?.Quotes?.FirstOrDefault() ?? batch?.Prices?.FirstOrDefault();
        return price is null
            ? new ShippingPrice(Cost: 0, Discount: null)
            : new ShippingPrice(price.CustomerPrice ?? 0, price.Discount);
    }

    private sealed record PricingBatchResponse(IReadOnlyList<PricingQuote>? Quotes, IReadOnlyList<PricingQuote>? Prices);

    private sealed record PricingQuote(decimal? CustomerPrice, decimal? LogisticsCost, decimal? Discount);
}
