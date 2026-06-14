using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Infrastructure.Messaging;

public sealed record ShippingQuoteRequestedPayload(
    Guid CheckoutId,
    Guid BuyerId,
    Guid SellerId,
    AddressDto Destination,
    IReadOnlyList<ShippingQuoteRequestedItemPayload> Items);

public sealed record ShippingQuoteRequestedItemPayload(
    Guid SkuId,
    Guid SellerId,
    int Quantity,
    decimal UnitPrice);
