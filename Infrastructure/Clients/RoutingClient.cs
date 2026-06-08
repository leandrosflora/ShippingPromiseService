using System.Net.Http.Json;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Infrastructure.Clients;

public sealed class RoutingClient : IRoutingClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RoutingClient> _logger;

    public RoutingClient(HttpClient httpClient, ILogger<RoutingClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RouteOption>> GetRoutesAsync(
        Guid originFulfillmentCenterId,
        AddressDto destination,
        PackageData package,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            OriginFulfillmentCenterId = originFulfillmentCenterId,
            Destination = destination,
            Package = package
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/routes/search",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Routing service failed with status {StatusCode}", response.StatusCode);
            return Array.Empty<RouteOption>();
        }

        var routes = await response.Content.ReadFromJsonAsync<List<RouteOption>>(cancellationToken);
        return routes ?? new List<RouteOption>();
    }
}
