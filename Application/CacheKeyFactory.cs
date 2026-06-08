using System.Security.Cryptography;
using System.Text;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application;

public static class CacheKeyFactory
{
    public static string Build(ShippingPromiseRequest request)
    {
        var items = string.Join(
            "|",
            request.Items
                .OrderBy(x => x.SkuId)
                .Select(x => $"{x.SkuId}:{x.Quantity}"));

        var raw = string.Join(
            ":",
            request.SellerId,
            request.Destination.ZipCode,
            request.Destination.State,
            request.Destination.Country,
            items);

        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

        return $"promise:{hash}";
    }
}
