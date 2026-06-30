using Microsoft.Extensions.Logging.Abstractions;
using ShippingPromiseService.Application;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.UnitTests.Application;

public sealed class ShippingPromiseApplicationServiceTests
{
    #region Validation

    [Fact]
    public async Task CalculateAsync_WithNullRequest_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(null!, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithEmptyBuyerId_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest() with { BuyerId = Guid.Empty };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithEmptySellerId_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest() with { SellerId = Guid.Empty };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithNullDestination_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest() with { Destination = null! };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithNullItems_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest() with { Items = null! };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithEmptyItems_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest() with { Items = [] };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithEmptyZipCode_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest() with { Destination = new AddressDto("", "São Paulo", "SP", "BR") };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithWhitespaceZipCode_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest() with { Destination = new AddressDto("   ", "São Paulo", "SP", "BR") };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithZeroQuantity_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest(items: [new ShippingPromiseItemDto(Guid.NewGuid(), 0, 10m)]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    [Fact]
    public async Task CalculateAsync_WithNegativeQuantity_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = MakeRequest(items: [new ShippingPromiseItemDto(Guid.NewGuid(), -1, 10m)]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CalculateAsync(request, "corr", CancellationToken.None));
    }

    #endregion

    #region Business rules

    [Fact]
    public async Task CalculateAsync_WhenProductCountMismatch_ReturnsUnavailable()
    {
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var catalog = new FakeProductCatalogClient
        {
            Products = [MakeProduct(sku1)]  // only 1 product returned for 2 requested skus
        };
        var service = CreateService(catalog: catalog);
        var request = MakeRequest(items: [
            new ShippingPromiseItemDto(sku1, 1, 10m),
            new ShippingPromiseItemDto(sku2, 1, 20m)
        ]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("Product information unavailable", result.UnavailableReason);
    }

    [Fact]
    public async Task CalculateAsync_WhenProductIsRestricted_ReturnsUnavailable()
    {
        var skuId = Guid.NewGuid();
        var catalog = new FakeProductCatalogClient
        {
            Products = [MakeProduct(skuId, isRestricted: true)]
        };
        var service = CreateService(catalog: catalog);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(skuId, 1, 10m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("Restricted item", result.UnavailableReason);
    }

    [Fact]
    public async Task CalculateAsync_WhenFulfillmentCenterHasNoCapacity_ReturnsUnavailable()
    {
        var skuId = Guid.NewGuid();
        var fcId = Guid.NewGuid();
        var catalog = new FakeProductCatalogClient { Products = [MakeProduct(skuId)] };
        var inventory = new FakeInventoryClient { Availability = [new InventoryAvailability(skuId, fcId, 10)] };
        var fulfillment = new FakeFulfillmentClient
        {
            Candidates = [new FulfillmentCandidate(fcId, "SP", new TimeOnly(23, 0), HasCapacity: false, CapacityScore: 0)]
        };
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(skuId, 1, 10m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("no_options_available", result.UnavailableReason);
    }

    [Fact]
    public async Task CalculateAsync_WhenInventoryNotInFulfillmentCenter_ReturnsUnavailable()
    {
        var skuId = Guid.NewGuid();
        var fcId = Guid.NewGuid();
        var otherFcId = Guid.NewGuid();
        var catalog = new FakeProductCatalogClient { Products = [MakeProduct(skuId)] };
        // inventory only exists in a different FC
        var inventory = new FakeInventoryClient { Availability = [new InventoryAvailability(skuId, otherFcId, 10)] };
        var fulfillment = new FakeFulfillmentClient
        {
            Candidates = [new FulfillmentCandidate(fcId, "SP", new TimeOnly(23, 0), HasCapacity: true, CapacityScore: 5)]
        };
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(skuId, 1, 10m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("no_options_available", result.UnavailableReason);
    }

    [Fact]
    public async Task CalculateAsync_WhenInventoryInsufficientForQuantity_ReturnsUnavailable()
    {
        var skuId = Guid.NewGuid();
        var fcId = Guid.NewGuid();
        var catalog = new FakeProductCatalogClient { Products = [MakeProduct(skuId)] };
        var inventory = new FakeInventoryClient { Availability = [new InventoryAvailability(skuId, fcId, 2)] };
        var fulfillment = new FakeFulfillmentClient
        {
            Candidates = [new FulfillmentCandidate(fcId, "SP", new TimeOnly(23, 0), HasCapacity: true, CapacityScore: 5)]
        };
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(skuId, 5, 10m)]); // requesting 5, only 2 available

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("no_options_available", result.UnavailableReason);
    }

    [Fact]
    public async Task CalculateAsync_WhenRouteUnavailable_ReturnsUnavailable()
    {
        var (catalog, inventory, fulfillment, _) = SetupHappyPathFakes();
        var routing = new FakeRoutingClient
        {
            Routes = [new RouteOption("r1", "n1", "n2", "CARRIER_A", "EXPRESS", 3, Available: false, Priority: 1)]
        };
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment, routing: routing);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(catalog.Products[0].SkuId, 1, 10m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("no_options_available", result.UnavailableReason);
    }

    [Fact]
    public async Task CalculateAsync_WhenCarrierUnavailable_ReturnsUnavailable()
    {
        var (catalog, inventory, fulfillment, routing) = SetupHappyPathFakes();
        var carrier = new FakeCarrierClient { IsAvailable = false };
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment, routing: routing, carrier: carrier);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(catalog.Products[0].SkuId, 1, 10m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("no_options_available", result.UnavailableReason);
    }

    [Fact]
    public async Task CalculateAsync_HappyPath_ReturnsCalculatedResponse()
    {
        var (catalog, inventory, fulfillment, routing) = SetupHappyPathFakes();
        var pricing = new FakePricingClient { Price = new ShippingPrice(Cost: 20m, Discount: 5m) };
        var audit = new FakeAuditRepository();
        var publisher = new FakeEventPublisher();
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment, routing: routing, pricing: pricing, audit: audit, publisher: publisher);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(catalog.Products[0].SkuId, 1, 100m)]);

        var result = await service.CalculateAsync(request, "corr-id", CancellationToken.None);

        Assert.True(result.Available);
        Assert.Equal("Calculated", result.Source);
        Assert.NotNull(result.PromiseId);
        Assert.StartsWith("promise_", result.PromiseId);
        Assert.Equal("CARRIER", result.Mode);      // route uses CARRIER_A, not LOGISTICA_ENVIOS
        Assert.Equal("CARRIER_A", result.Carrier);
        Assert.Equal(15m, result.Cost);            // 20 - 5 = 15
        Assert.Null(result.UnavailableReason);
    }

    [Fact]
    public async Task CalculateAsync_HappyPath_SavesAuditAndPublishesEvent()
    {
        var (catalog, inventory, fulfillment, routing) = SetupHappyPathFakes();
        var audit = new FakeAuditRepository();
        var publisher = new FakeEventPublisher();
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment, routing: routing, audit: audit, publisher: publisher);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(catalog.Products[0].SkuId, 1, 100m)]);

        await service.CalculateAsync(request, "corr-id", CancellationToken.None);

        Assert.Equal(1, audit.SaveCalls);
        Assert.Equal(1, publisher.PublishCalls);
    }

    [Fact]
    public async Task CalculateAsync_WhenCarrierIsLogisticaEnvios_SetsModeToFulfillment()
    {
        var skuId = Guid.NewGuid();
        var fcId = Guid.NewGuid();
        var catalog = new FakeProductCatalogClient { Products = [MakeProduct(skuId)] };
        var inventory = new FakeInventoryClient { Availability = [new InventoryAvailability(skuId, fcId, 10)] };
        var fulfillment = new FakeFulfillmentClient
        {
            Candidates = [new FulfillmentCandidate(fcId, "SP", new TimeOnly(23, 0), HasCapacity: true, CapacityScore: 5)]
        };
        var routing = new FakeRoutingClient
        {
            Routes = [new RouteOption("r1", "n1", "n2", "LOGISTICA_ENVIOS", "STANDARD", 3, Available: true, Priority: 1)]
        };
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment, routing: routing);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(skuId, 1, 10m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.True(result.Available);
        Assert.Equal("FULFILLMENT", result.Mode);
    }

    [Fact]
    public async Task CalculateAsync_ShippingCostIsNetOfDiscount()
    {
        var (catalog, inventory, fulfillment, routing) = SetupHappyPathFakes();
        var pricing = new FakePricingClient { Price = new ShippingPrice(Cost: 100m, Discount: 30m) };
        var service = CreateService(catalog: catalog, inventory: inventory, fulfillment: fulfillment, routing: routing, pricing: pricing);
        var request = MakeRequest(items: [new ShippingPromiseItemDto(catalog.Products[0].SkuId, 1, 100m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.Equal(70m, result.Cost);  // 100 - 30
    }

    [Fact]
    public async Task CalculateAsync_WhenExceptionAndCountryIsBR_ReturnsFallback()
    {
        var catalog = new ThrowingProductCatalogClient(new HttpRequestException("timeout"));
        var audit = new FakeAuditRepository();
        var publisher = new FakeEventPublisher();
        var service = CreateService(catalog: catalog, audit: audit, publisher: publisher);
        var request = MakeRequest(country: "BR", items: [new ShippingPromiseItemDto(Guid.NewGuid(), 1, 10m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.True(result.Available);
        Assert.Equal("Fallback", result.Source);
        Assert.Equal("DEFAULT_CARRIER", result.Carrier);
        Assert.Equal(1, audit.SaveCalls);
        Assert.Equal(1, publisher.PublishCalls);
    }

    [Fact]
    public async Task CalculateAsync_WhenExceptionAndCountryIsNotBR_ReturnsUnavailable()
    {
        var catalog = new ThrowingProductCatalogClient(new Exception("network error"));
        var audit = new FakeAuditRepository();
        var service = CreateService(catalog: catalog, audit: audit);
        var request = MakeRequest(country: "AR", items: [new ShippingPromiseItemDto(Guid.NewGuid(), 1, 10m)]);

        var result = await service.CalculateAsync(request, "corr", CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("Shipping promise temporarily unavailable", result.UnavailableReason);
        Assert.Equal(0, audit.SaveCalls);
    }

    #endregion

    #region GetCalculatedPromiseAsync

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCalculatedPromiseAsync_WithEmptyOrNullPromiseId_ThrowsArgumentException(string? promiseId)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetCalculatedPromiseAsync(promiseId!, CancellationToken.None));
    }

    [Fact]
    public async Task GetCalculatedPromiseAsync_WithValidId_DelegatesToAuditRepository()
    {
        var expected = new ShippingPromiseResponse(
            true, "promise_abc", "CARRIER", "CARRIER_A",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), 15m, "Calculated", null);
        var audit = new FakeAuditRepository { GetResult = expected };
        var service = CreateService(audit: audit);

        var result = await service.GetCalculatedPromiseAsync("promise_abc", CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetCalculatedPromiseAsync_WhenNotFound_ReturnsNull()
    {
        var audit = new FakeAuditRepository { GetResult = null };
        var service = CreateService(audit: audit);

        var result = await service.GetCalculatedPromiseAsync("unknown-id", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region Helpers

    private static (FakeProductCatalogClient catalog, FakeInventoryClient inventory, FakeFulfillmentClient fulfillment, FakeRoutingClient routing)
        SetupHappyPathFakes()
    {
        var skuId = Guid.NewGuid();
        var fcId = Guid.NewGuid();
        var catalog = new FakeProductCatalogClient { Products = [MakeProduct(skuId)] };
        var inventory = new FakeInventoryClient { Availability = [new InventoryAvailability(skuId, fcId, 10)] };
        var fulfillment = new FakeFulfillmentClient
        {
            Candidates = [new FulfillmentCandidate(fcId, "SP", new TimeOnly(23, 0), HasCapacity: true, CapacityScore: 5)]
        };
        var routing = new FakeRoutingClient
        {
            Routes = [new RouteOption("r1", "n1", "n2", "CARRIER_A", "EXPRESS", 3, Available: true, Priority: 1)]
        };
        return (catalog, inventory, fulfillment, routing);
    }

    private static ProductPhysicalInfo MakeProduct(
        Guid? skuId = null,
        bool isRestricted = false,
        bool isFragile = false) =>
        new(skuId ?? Guid.NewGuid(), 1m, 10m, 10m, 10m, "electronics", isFragile, isRestricted);

    private static ShippingPromiseRequest MakeRequest(
        Guid? sellerId = null,
        string country = "BR",
        IReadOnlyList<ShippingPromiseItemDto>? items = null) =>
        new(
            CheckoutId: null,
            BuyerId: Guid.NewGuid(),
            SellerId: sellerId ?? Guid.NewGuid(),
            Destination: new AddressDto("01310-100", "São Paulo", "SP", country),
            Items: items ?? [new ShippingPromiseItemDto(Guid.NewGuid(), 1, 50m)]
        );

    private static ShippingPromiseApplicationService CreateService(
        IProductCatalogClient? catalog = null,
        IInventoryClient? inventory = null,
        IFulfillmentClient? fulfillment = null,
        IRoutingClient? routing = null,
        ICarrierClient? carrier = null,
        IPricingClient? pricing = null,
        IShippingPromiseAuditRepository? audit = null,
        IShippingPromiseEventPublisher? publisher = null) =>
        new(
            cache: new FakeCache(),
            productCatalogClient: catalog ?? new FakeProductCatalogClient(),
            inventoryClient: inventory ?? new FakeInventoryClient(),
            fulfillmentClient: fulfillment ?? new FakeFulfillmentClient(),
            routingClient: routing ?? new FakeRoutingClient(),
            carrierClient: carrier ?? new FakeCarrierClient(),
            pricingClient: pricing ?? new FakePricingClient(),
            packageCalculator: new PackageCalculator(),
            decisionEngine: new DeliveryDecisionEngine(),
            fallbackEngine: new FallbackEngine(),
            auditRepository: audit ?? new FakeAuditRepository(),
            eventPublisher: publisher ?? new FakeEventPublisher(),
            logger: NullLogger<ShippingPromiseApplicationService>.Instance
        );

    #endregion

    #region Fakes

    private sealed class FakeCache : IShippingPromiseCache
    {
        public Task<ShippingPromiseResponse?> GetAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult<ShippingPromiseResponse?>(null);

        public Task SetAsync(string key, ShippingPromiseResponse response, TimeSpan ttl, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeProductCatalogClient : IProductCatalogClient
    {
        public IReadOnlyList<ProductPhysicalInfo> Products { get; set; } = [];

        public Task<IReadOnlyList<ProductPhysicalInfo>> GetProductsAsync(IReadOnlyList<Guid> skuIds, CancellationToken cancellationToken)
            => Task.FromResult(Products);
    }

    private sealed class FakeInventoryClient : IInventoryClient
    {
        public IReadOnlyList<InventoryAvailability> Availability { get; set; } = [];

        public Task<IReadOnlyList<InventoryAvailability>> GetAvailabilityAsync(Guid sellerId, IReadOnlyList<Guid> skuIds, CancellationToken cancellationToken)
            => Task.FromResult(Availability);
    }

    private sealed class FakeFulfillmentClient : IFulfillmentClient
    {
        public IReadOnlyList<FulfillmentCandidate> Candidates { get; set; } = [];

        public Task<IReadOnlyList<FulfillmentCandidate>> GetCandidatesAsync(Guid sellerId, AddressDto destination, PackageData package, CancellationToken cancellationToken)
            => Task.FromResult(Candidates);
    }

    private sealed class FakeRoutingClient : IRoutingClient
    {
        public IReadOnlyList<RouteOption> Routes { get; set; } = [];

        public Task<IReadOnlyList<RouteOption>> GetRoutesAsync(Guid originFulfillmentCenterId, AddressDto destination, PackageData package, CancellationToken cancellationToken)
            => Task.FromResult(Routes);
    }

    private sealed class FakeCarrierClient : ICarrierClient
    {
        public bool IsAvailable { get; set; } = true;

        public Task<bool> IsCarrierAvailableAsync(RouteOption route, AddressDto destination, PackageData package, CancellationToken cancellationToken)
            => Task.FromResult(IsAvailable);
    }

    private sealed class FakePricingClient : IPricingClient
    {
        public ShippingPrice Price { get; set; } = new(Cost: 15m, Discount: null);

        public Task<ShippingPrice> GetPriceAsync(ShippingPromiseRequest request, ShippingMode mode, RouteOption route, PackageData package, string packageCategory, CancellationToken cancellationToken)
            => Task.FromResult(Price);
    }

    private sealed class FakeAuditRepository : IShippingPromiseAuditRepository
    {
        public int SaveCalls { get; private set; }
        public ShippingPromiseResponse? GetResult { get; set; }

        public Task<ShippingPromiseResponse?> GetByPromiseIdAsync(string promiseId, CancellationToken cancellationToken)
            => Task.FromResult(GetResult);

        public Task SaveAsync(ShippingPromiseRequest request, ShippingPromiseResponse response, IReadOnlyList<DeliveryCandidate> candidates, CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventPublisher : IShippingPromiseEventPublisher
    {
        public int PublishCalls { get; private set; }

        public Task PublishCalculatedAsync(ShippingPromiseRequest request, ShippingPromiseResponse response, string correlationId, CancellationToken cancellationToken)
        {
            PublishCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingProductCatalogClient : IProductCatalogClient
    {
        private readonly Exception _exception;

        public ThrowingProductCatalogClient(Exception exception) => _exception = exception;

        public Task<IReadOnlyList<ProductPhysicalInfo>> GetProductsAsync(IReadOnlyList<Guid> skuIds, CancellationToken cancellationToken)
            => throw _exception;
    }

    #endregion
}
