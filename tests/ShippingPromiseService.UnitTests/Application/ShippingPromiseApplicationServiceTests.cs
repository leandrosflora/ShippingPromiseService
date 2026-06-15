using Microsoft.Extensions.Logging.Abstractions;
using ShippingPromiseService.Application;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.UnitTests.Application;

public sealed class ShippingPromiseApplicationServiceTests
{
    [Fact]
    public async Task CalculateAsync_WhenCacheHit_ReturnsCacheSourceAndDoesNotCallDownstreamClients()
    {
        var cache = new FakeCache
        {
            Response = new ShippingPromiseResponse(true, "promise_cached", "FULFILLMENT", "MELI_LOGISTICS", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 12.30m, "Calculated", null)
        };
        var eventPublisher = new FakeEventPublisher();
        var productCatalog = new FakeProductCatalogClient();
        var service = CreateService(cache: cache, productCatalog: productCatalog, eventPublisher: eventPublisher);
        var request = ValidRequest(checkoutId: Guid.NewGuid());

        var response = await service.CalculateAsync(request, "corr-1", CancellationToken.None);

        Assert.True(response.Available);
        Assert.Equal("Cache", response.Source);
        Assert.Equal("promise_cached", response.PromiseId);
        Assert.Equal(1, eventPublisher.PublishCount);
        Assert.Equal(0, productCatalog.CallCount);
    }

    [Fact]
    public async Task CalculateAsync_WhenDependenciesReturnValidOptions_CalculatesCachesAuditsAndPublishesKafkaContractEvent()
    {
        var skuId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var fulfillmentCenterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var cache = new FakeCache();
        var audit = new FakeAuditRepository();
        var eventPublisher = new FakeEventPublisher();
        var service = CreateService(
            cache: cache,
            productCatalog: new FakeProductCatalogClient(new ProductPhysicalInfo(skuId, 1m, 10m, 10m, 10m, "books", false, false)),
            inventory: new FakeInventoryClient(new InventoryAvailability(skuId, fulfillmentCenterId, 3)),
            fulfillment: new FakeFulfillmentClient(new FulfillmentCandidate(fulfillmentCenterId, "SP", TimeOnly.MaxValue, true, 10)),
            routing: new FakeRoutingClient(new RouteOption("route-1", "origin", "dest", "MELI_LOGISTICS", "standard", 2, true, 5)),
            carrier: new FakeCarrierClient(true),
            pricing: new FakePricingClient(new ShippingPrice(20m, 5m)),
            audit: audit,
            eventPublisher: eventPublisher);
        var request = ValidRequest(skuId: skuId, quantity: 2, checkoutId: Guid.NewGuid());

        var response = await service.CalculateAsync(request, "corr-2", CancellationToken.None);

        Assert.True(response.Available);
        Assert.Equal("FULFILLMENT", response.Mode);
        Assert.Equal("MELI_LOGISTICS", response.Carrier);
        Assert.Equal(15m, response.Cost);
        Assert.Equal("Calculated", response.Source);
        Assert.Equal(1, cache.SetCount);
        Assert.Equal(1, audit.SaveCount);
        Assert.Single(audit.LastCandidates);
        Assert.Equal(1, eventPublisher.PublishCount);
        Assert.Equal("corr-2", eventPublisher.LastCorrelationId);
    }

    [Fact]
    public async Task CalculateAsync_WhenProductIsRestricted_ReturnsUnavailableWithoutAuditOrEvent()
    {
        var skuId = Guid.NewGuid();
        var audit = new FakeAuditRepository();
        var eventPublisher = new FakeEventPublisher();
        var service = CreateService(
            productCatalog: new FakeProductCatalogClient(new ProductPhysicalInfo(skuId, 1m, 1m, 1m, 1m, "restricted", false, true)),
            inventory: new FakeInventoryClient(),
            audit: audit,
            eventPublisher: eventPublisher);

        var response = await service.CalculateAsync(ValidRequest(skuId: skuId), "corr-3", CancellationToken.None);

        Assert.False(response.Available);
        Assert.Equal("Restricted item", response.UnavailableReason);
        Assert.Equal(0, audit.SaveCount);
        Assert.Equal(0, eventPublisher.PublishCount);
    }

    [Fact]
    public async Task CalculateAsync_WhenDownstreamFailsForBrazil_ReturnsFallbackAndPublishesWithoutRealHttpKafkaOrDatabase()
    {
        var audit = new FakeAuditRepository();
        var eventPublisher = new FakeEventPublisher();
        var service = CreateService(productCatalog: new ThrowingProductCatalogClient(), audit: audit, eventPublisher: eventPublisher);

        var response = await service.CalculateAsync(ValidRequest(), "corr-4", CancellationToken.None);

        Assert.True(response.Available);
        Assert.Equal("Fallback", response.Source);
        Assert.Equal("SELLERSHIPPING", response.Mode);
        Assert.Equal("DEFAULT_CARRIER", response.Carrier);
        Assert.Equal(29.90m, response.Cost);
        Assert.Equal(1, audit.SaveCount);
        Assert.Equal(1, eventPublisher.PublishCount);
    }

    [Fact]
    public async Task CalculateAsync_WhenRequestViolatesRestContractValidation_ThrowsBeforeDependencies()
    {
        var service = CreateService();
        var invalid = ValidRequest() with { BuyerId = Guid.Empty };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CalculateAsync(invalid, "corr-5", CancellationToken.None));

        Assert.Equal("BuyerId is required", exception.Message);
    }

    private static ShippingPromiseApplicationService CreateService(
        IShippingPromiseCache? cache = null,
        IProductCatalogClient? productCatalog = null,
        IInventoryClient? inventory = null,
        IFulfillmentClient? fulfillment = null,
        IRoutingClient? routing = null,
        ICarrierClient? carrier = null,
        IPricingClient? pricing = null,
        IShippingPromiseAuditRepository? audit = null,
        IShippingPromiseEventPublisher? eventPublisher = null) => new(
            cache ?? new FakeCache(),
            productCatalog ?? new FakeProductCatalogClient(),
            inventory ?? new FakeInventoryClient(),
            fulfillment ?? new FakeFulfillmentClient(),
            routing ?? new FakeRoutingClient(),
            carrier ?? new FakeCarrierClient(true),
            pricing ?? new FakePricingClient(new ShippingPrice(10m, null)),
            new PackageCalculator(),
            new DeliveryDecisionEngine(),
            new FallbackEngine(),
            audit ?? new FakeAuditRepository(),
            eventPublisher ?? new FakeEventPublisher(),
            NullLogger<ShippingPromiseApplicationService>.Instance);

    private static ShippingPromiseRequest ValidRequest(Guid? skuId = null, int quantity = 1, Guid? checkoutId = null) =>
        new(checkoutId, Guid.NewGuid(), Guid.NewGuid(), new AddressDto("01310-100", "São Paulo", "SP", "BR"), new[]
        {
            new ShippingPromiseItemDto(skuId ?? Guid.Parse("33333333-3333-3333-3333-333333333333"), quantity, 199.90m)
        });

    private sealed class FakeCache : IShippingPromiseCache
    {
        public ShippingPromiseResponse? Response { get; init; }
        public int SetCount { get; private set; }
        public Task<ShippingPromiseResponse?> GetAsync(string key, CancellationToken cancellationToken) => Task.FromResult(Response);
        public Task SetAsync(string key, ShippingPromiseResponse response, TimeSpan ttl, CancellationToken cancellationToken) { SetCount++; return Task.CompletedTask; }
    }

    private sealed class FakeProductCatalogClient(params ProductPhysicalInfo[] products) : IProductCatalogClient
    {
        public int CallCount { get; private set; }
        public Task<IReadOnlyList<ProductPhysicalInfo>> GetProductsAsync(IReadOnlyList<Guid> skuIds, CancellationToken cancellationToken) { CallCount++; return Task.FromResult<IReadOnlyList<ProductPhysicalInfo>>(products); }
    }

    private sealed class ThrowingProductCatalogClient : IProductCatalogClient
    {
        public Task<IReadOnlyList<ProductPhysicalInfo>> GetProductsAsync(IReadOnlyList<Guid> skuIds, CancellationToken cancellationToken) => throw new TimeoutException("product catalog timeout");
    }

    private sealed class FakeInventoryClient(params InventoryAvailability[] availability) : IInventoryClient
    {
        public Task<IReadOnlyList<InventoryAvailability>> GetAvailabilityAsync(Guid sellerId, IReadOnlyList<Guid> skuIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<InventoryAvailability>>(availability);
    }

    private sealed class FakeFulfillmentClient(params FulfillmentCandidate[] candidates) : IFulfillmentClient
    {
        public Task<IReadOnlyList<FulfillmentCandidate>> GetCandidatesAsync(Guid sellerId, AddressDto destination, PackageData package, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FulfillmentCandidate>>(candidates);
    }

    private sealed class FakeRoutingClient(params RouteOption[] routes) : IRoutingClient
    {
        public Task<IReadOnlyList<RouteOption>> GetRoutesAsync(Guid originFulfillmentCenterId, AddressDto destination, PackageData package, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RouteOption>>(routes);
    }

    private sealed class FakeCarrierClient(bool available) : ICarrierClient
    {
        public Task<bool> IsCarrierAvailableAsync(RouteOption route, AddressDto destination, PackageData package, CancellationToken cancellationToken) => Task.FromResult(available);
    }

    private sealed class FakePricingClient(ShippingPrice price) : IPricingClient
    {
        public Task<ShippingPrice> GetPriceAsync(ShippingPromiseRequest request, ShippingMode mode, RouteOption route, PackageData package, string packageCategory, CancellationToken cancellationToken) => Task.FromResult(price);
    }

    private sealed class FakeAuditRepository : IShippingPromiseAuditRepository
    {
        public int SaveCount { get; private set; }
        public IReadOnlyList<DeliveryCandidate> LastCandidates { get; private set; } = Array.Empty<DeliveryCandidate>();
        public Task SaveAsync(ShippingPromiseRequest request, ShippingPromiseResponse response, IReadOnlyList<DeliveryCandidate> candidates, CancellationToken cancellationToken) { SaveCount++; LastCandidates = candidates; return Task.CompletedTask; }
    }

    private sealed class FakeEventPublisher : IShippingPromiseEventPublisher
    {
        public int PublishCount { get; private set; }
        public string? LastCorrelationId { get; private set; }
        public Task PublishCalculatedAsync(ShippingPromiseRequest request, ShippingPromiseResponse response, string correlationId, CancellationToken cancellationToken) { PublishCount++; LastCorrelationId = correlationId; return Task.CompletedTask; }
    }
}
