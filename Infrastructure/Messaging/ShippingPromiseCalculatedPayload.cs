using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Infrastructure.Messaging;

public sealed record ShippingPromiseCalculatedPayload(
    Guid BuyerId,
    Guid SellerId,
    AddressDto Destination,
    IReadOnlyList<ShippingPromiseItemDto> Items,
    string PromiseId,
    string Mode,
    string Carrier,
    DateOnly EstimatedDeliveryDate,
    decimal Cost,
    string Source);
