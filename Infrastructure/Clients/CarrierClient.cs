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
        string carrier,
        AddressDto destination,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/carriers/availability",
            new { Carrier = carrier, Destination = destination },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Carrier service failed with status {StatusCode}", response.StatusCode);
            return false;
        }

        var availability = await response.Content.ReadFromJsonAsync<CarrierAvailabilityResponse>(cancellationToken);
        return availability?.Available ?? false;
    }

    private sealed record CarrierAvailabilityResponse(bool Available);
}
