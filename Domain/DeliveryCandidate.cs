namespace ShippingPromiseService.Domain;

public sealed record DeliveryCandidate(
    ShippingMode Mode,
    Guid OriginFulfillmentCenterId,
    string Carrier,
    DateOnly EstimatedDeliveryDate,
    decimal ShippingCost,
    int Priority,
    bool IsFallback
);
