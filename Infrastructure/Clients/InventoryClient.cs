using System.Net.Http.Json;
using ShippingPromiseService.Application.Ports;

namespace ShippingPromiseService.Infrastructure.Clients;

public sealed class InventoryClient : IInventoryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InventoryClient> _logger;

    public InventoryClient(HttpClient httpClient, ILogger<InventoryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InventoryAvailability>> GetAvailabilityAsync(
        Guid sellerId,
        IReadOnlyList<Guid> skuIds,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/v1/inventory/availability/batch",
            new { sellerId, skuIds },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Inventory service failed with status {StatusCode}", response.StatusCode);
            return Array.Empty<InventoryAvailability>();
        }

        var availability = await response.Content.ReadFromJsonAsync<List<InventoryAvailability>>(cancellationToken);
        return availability ?? new List<InventoryAvailability>();
    }
}
