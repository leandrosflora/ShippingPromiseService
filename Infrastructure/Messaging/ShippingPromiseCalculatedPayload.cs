namespace ShippingPromiseService.Infrastructure.Messaging;

public sealed record ShippingPromiseCalculatedPayload(
    Guid CheckoutId,
    Guid BuyerId,
    Guid SellerId,
    string PromiseId,
    string Mode,
    string Carrier,
    DateOnly EstimatedDeliveryDate,
    decimal Cost,
    string Currency,
    string Source);
