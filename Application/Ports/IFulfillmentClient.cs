using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application.Ports;

public interface IFulfillmentClient
{
    Task<IReadOnlyList<FulfillmentCandidate>> GetCandidatesAsync(
        Guid sellerId,
        AddressDto destination,
        PackageData package,
        CancellationToken cancellationToken);
}

public sealed record FulfillmentCandidate(
    Guid FulfillmentCenterId,
    string Region,
    TimeOnly CutoffTime,
    bool HasCapacity,
    int CapacityScore
);
