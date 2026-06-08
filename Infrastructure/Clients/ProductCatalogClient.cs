using System.Net.Http.Json;
using ShippingPromiseService.Application.Ports;

namespace ShippingPromiseService.Infrastructure.Clients;

public sealed class ProductCatalogClient : IProductCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductCatalogClient> _logger;

    public ProductCatalogClient(HttpClient httpClient, ILogger<ProductCatalogClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductPhysicalInfo>> GetProductsAsync(
        IReadOnlyList<Guid> skuIds,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/products/physical-info/batch",
            new { SkuIds = skuIds },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Product Catalog service failed with status {StatusCode}", response.StatusCode);
            return Array.Empty<ProductPhysicalInfo>();
        }

        var products = await response.Content.ReadFromJsonAsync<List<ProductPhysicalInfo>>(cancellationToken);
        return products ?? new List<ProductPhysicalInfo>();
    }
}
