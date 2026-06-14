using ShippingPromiseService.Application.Ports;

namespace ShippingPromiseService.Infrastructure.Clients;

internal static class DownstreamContractAdapters
{
    public static object ToPackageProfile(PackageData package) => new
    {
        weightKg = package.TotalWeightKg,
        cubicWeightKg = package.CubicWeightKg,
        isFragile = package.HasFragileItem,
        isRestricted = package.HasRestrictedItem
    };
}
