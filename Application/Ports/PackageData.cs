namespace ShippingPromiseService.Application.Ports;

public sealed record PackageData(
    decimal TotalWeightKg,
    decimal CubicWeightKg,
    decimal HeightCm,
    decimal WidthCm,
    decimal LengthCm,
    bool HasFragileItem,
    bool HasRestrictedItem
);
