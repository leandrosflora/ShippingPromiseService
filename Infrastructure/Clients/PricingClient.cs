using System.Net.Http.Json;
using ShippingPromiseService.Application.Ports;
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
        ShippingMode mode,
        RouteOption route,
        PackageData package,
        CancellationToken cancellationToken)
    {
        var candidateId = string.IsNullOrWhiteSpace(route.RouteId)
            ? $"{route.OriginNodeId}:{route.CarrierCode}:{route.ServiceLevelCode}"
            : route.RouteId;

        var request = new
        {
            quotes = new[]
            {
                new
                {
                    candidateId,
                    routeId = route.RouteId,
                    originNodeId = route.OriginNodeId,
                    carrierCode = route.CarrierCode,
                    serviceLevelCode = route.ServiceLevelCode,
                    mode = mode.ToString(),
                    package = DownstreamContractAdapters.ToPackageProfile(package)
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/shipping-prices/quotes/batch",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Pricing service failed with status {StatusCode}", response.StatusCode);
            return new ShippingPrice(Cost: 0, Discount: null);
        }

        var batch = await response.Content.ReadFromJsonAsync<PricingBatchResponse>(cancellationToken);
        var price = batch?.Quotes?.FirstOrDefault() ?? batch?.Prices?.FirstOrDefault();
        return price is null ? new ShippingPrice(Cost: 0, Discount: null) : new ShippingPrice(price.Cost, price.Discount);
    }

    private sealed record PricingBatchResponse(IReadOnlyList<PricingQuote>? Quotes, IReadOnlyList<PricingQuote>? Prices);

    private sealed record PricingQuote(decimal Cost, decimal? Discount);
}
