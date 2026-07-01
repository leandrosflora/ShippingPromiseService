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
            originNodeId = originFulfillmentCenterId,
            destinationPostalCode = destination.ZipCode,
            package = DownstreamContractAdapters.ToPackageProfile(package),
            requestedAtUtc = DateTimeOffset.UtcNow,
            maxOptions = 3
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/v1/routes/calculate",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Routing service failed with status {StatusCode}", response.StatusCode);
            return Array.Empty<RouteOption>();
        }

        var searchResponse = await response.Content.ReadFromJsonAsync<SearchRoutesResponse>(cancellationToken);

        return searchResponse?.Routes?
            .Select((route, index) => MapRoute(route, index))
            .Where(route => !string.IsNullOrWhiteSpace(route.CarrierCode))
            .ToList() ?? new List<RouteOption>();
    }

    private static RouteOption MapRoute(RouteResponse route, int index)
    {
        var firstLeg = route.Legs?.FirstOrDefault();

        return new RouteOption(
            route.RouteId,
            route.OriginNodeId,
            route.DestinationNodeId,
            firstLeg?.CarrierCode ?? string.Empty,
            firstLeg?.ServiceLevelCode ?? string.Empty,
            (int)Math.Ceiling(route.TotalElapsedMinutes / 1440m),
            Available: true,
            Priority: index);
    }

    private sealed record SearchRoutesResponse(IReadOnlyList<RouteResponse>? Routes);

    private sealed record RouteResponse(
        string RouteId,
        string OriginNodeId,
        string? DestinationNodeId,
        int TotalElapsedMinutes,
        IReadOnlyList<RouteLegResponse>? Legs);

    private sealed record RouteLegResponse(
        string? CarrierCode,
        string? ServiceLevelCode);
}
