using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
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
        var query = skuIds.Select(skuId => new KeyValuePair<string, string?>("skuIds", skuId.ToString()));
        var requestUri = QueryHelpers.AddQueryString("/v1/products/logistics/batch", query);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Product Catalog service failed with status {StatusCode}", response.StatusCode);
            return Array.Empty<ProductPhysicalInfo>();
        }

        var products = await response.Content.ReadFromJsonAsync<List<ProductPhysicalInfo>>(cancellationToken);
        return products ?? new List<ProductPhysicalInfo>();
    }
}
