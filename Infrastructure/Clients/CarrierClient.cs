using System.Net.Http.Json;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Infrastructure.Clients;

public sealed class CarrierClient : ICarrierClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CarrierClient> _logger;

    public CarrierClient(HttpClient httpClient, ILogger<CarrierClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IsCarrierAvailableAsync(
        RouteOption route,
        AddressDto destination,
        PackageData package,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            checks = new[]
            {
                new
                {
                    checkId = $"{route.RouteId}:{route.CarrierCode}:{route.ServiceLevelCode}",
                    carrierCode = route.CarrierCode,
                    serviceLevelCode = route.ServiceLevelCode,
                    originNodeId = route.OriginNodeId,
                    destinationNodeId = route.DestinationNodeId,
                    destinationPostalCode = destination.ZipCode,
                    plannedDepartureAtUtc = DateTimeOffset.UtcNow,
                    package = DownstreamContractAdapters.ToPackageProfile(package)
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/v1/carrier-availability/search",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Carrier service failed with status {StatusCode}", response.StatusCode);
            return false;
        }

        var availability = await response.Content.ReadFromJsonAsync<CarrierAvailabilityBatchResponse>(cancellationToken);
        return availability?.Available ?? availability?.Results?.Any(x => x.Available) ?? false;
    }

    private sealed record CarrierAvailabilityBatchResponse(bool? Available, IReadOnlyList<CarrierAvailabilityResult>? Results);

    private sealed record CarrierAvailabilityResult(bool Available);
}
