namespace ShippingPromiseService.Contracts;

public sealed record ShippingPromiseResponse(
    bool Available,
    string? PromiseId,
    string? Mode,
    string? Carrier,
    DateOnly? EstimatedDeliveryDate,
    decimal? Cost,
    string Source,
    string? UnavailableReason
);
