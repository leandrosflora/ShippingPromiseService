using System.Net.Http.Json;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Infrastructure.Clients;

public sealed class FulfillmentClient : IFulfillmentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FulfillmentClient> _logger;

    public FulfillmentClient(HttpClient httpClient, ILogger<FulfillmentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FulfillmentCandidate>> GetCandidatesAsync(
        Guid sellerId,
        AddressDto destination,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/fulfillment/candidates",
            new { SellerId = sellerId, Destination = destination },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Fulfillment service failed with status {StatusCode}", response.StatusCode);
            return Array.Empty<FulfillmentCandidate>();
        }

        var candidates = await response.Content.ReadFromJsonAsync<List<FulfillmentCandidate>>(cancellationToken);
        return candidates ?? new List<FulfillmentCandidate>();
    }
}
