using System.Net.Http.Json;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

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
        PackageData package,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            sellerId,
            destinationPostalCode = destination.ZipCode,
            mode = (int)ShippingMode.Fulfillment,
            package = DownstreamContractAdapters.ToPackageProfile(package),
            requestedAtUtc = DateTimeOffset.UtcNow
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/fulfillment-centers/candidates/search",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Fulfillment service failed with status {StatusCode}", response.StatusCode);
            return Array.Empty<FulfillmentCandidate>();
        }

        var candidates = await response.Content.ReadFromJsonAsync<List<FulfillmentCenterCandidateResponse>>(cancellationToken);

        return candidates?
            .Select(x => new FulfillmentCandidate(
                x.FulfillmentCenterId,
                x.Region,
                TimeOnly.FromTimeSpan(x.CutoffAt.TimeOfDay),
                x.AvailableCapacityUnits > 0,
                x.Score))
            .ToList() ?? new List<FulfillmentCandidate>();
    }

    private sealed record FulfillmentCenterCandidateResponse(
        Guid FulfillmentCenterId,
        string Region,
        DateTimeOffset CutoffAt,
        int AvailableCapacityUnits,
        int Score);
}
