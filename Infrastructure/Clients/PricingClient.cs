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
        string carrier,
        PackageData package,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/shipping/prices/quote",
            new { Mode = mode.ToString(), Carrier = carrier, Package = package },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Pricing service failed with status {StatusCode}", response.StatusCode);
            return new ShippingPrice(Cost: 0, Discount: null);
        }

        var price = await response.Content.ReadFromJsonAsync<ShippingPrice>(cancellationToken);
        return price ?? new ShippingPrice(Cost: 0, Discount: null);
    }
}
