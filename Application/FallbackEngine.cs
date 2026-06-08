using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Application;

public sealed class FallbackEngine
{
    public DeliveryCandidate? TryBuildFallback(
        ShippingPromiseRequest request,
        Exception exception)
    {
        if (!string.Equals(request.Destination.Country, "BR", StringComparison.OrdinalIgnoreCase))
            return null;

        return new DeliveryCandidate(
            Mode: ShippingMode.SellerShipping,
            OriginFulfillmentCenterId: Guid.Empty,
            Carrier: "DEFAULT_CARRIER",
            EstimatedDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            ShippingCost: 29.90m,
            Priority: 999,
            IsFallback: true
        );
    }
}
