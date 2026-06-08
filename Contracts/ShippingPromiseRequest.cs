namespace ShippingPromiseService.Contracts;

public sealed record ShippingPromiseRequest(
    Guid BuyerId,
    Guid SellerId,
    AddressDto Destination,
    IReadOnlyList<ShippingPromiseItemDto> Items
);

public sealed record ShippingPromiseItemDto(
    Guid SkuId,
    int Quantity,
    decimal UnitPrice
);

public sealed record AddressDto(
    string ZipCode,
    string City,
    string State,
    string Country
);
