using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Application;

public sealed class ShippingPromiseApplicationService
{
    private static readonly TimeSpan PromiseCacheTtl = TimeSpan.FromSeconds(60);

    private readonly IShippingPromiseCache _cache;
    private readonly IProductCatalogClient _productCatalogClient;
    private readonly IInventoryClient _inventoryClient;
    private readonly IFulfillmentClient _fulfillmentClient;
    private readonly IRoutingClient _routingClient;
    private readonly ICarrierClient _carrierClient;
    private readonly IPricingClient _pricingClient;
    private readonly PackageCalculator _packageCalculator;
    private readonly DeliveryDecisionEngine _decisionEngine;
    private readonly FallbackEngine _fallbackEngine;
    private readonly IShippingPromiseAuditRepository _auditRepository;
    private readonly IShippingPromiseEventPublisher _eventPublisher;
    private readonly ILogger<ShippingPromiseApplicationService> _logger;

    public ShippingPromiseApplicationService(
        IShippingPromiseCache cache,
        IProductCatalogClient productCatalogClient,
        IInventoryClient inventoryClient,
        IFulfillmentClient fulfillmentClient,
        IRoutingClient routingClient,
        ICarrierClient carrierClient,
        IPricingClient pricingClient,
        PackageCalculator packageCalculator,
        DeliveryDecisionEngine decisionEngine,
        FallbackEngine fallbackEngine,
        IShippingPromiseAuditRepository auditRepository,
        IShippingPromiseEventPublisher eventPublisher,
        ILogger<ShippingPromiseApplicationService> logger)
    {
        _cache = cache;
        _productCatalogClient = productCatalogClient;
        _inventoryClient = inventoryClient;
        _fulfillmentClient = fulfillmentClient;
        _routingClient = routingClient;
        _carrierClient = carrierClient;
        _pricingClient = pricingClient;
        _packageCalculator = packageCalculator;
        _decisionEngine = decisionEngine;
        _fallbackEngine = fallbackEngine;
        _auditRepository = auditRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<ShippingPromiseResponse> CalculateAsync(
        ShippingPromiseRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var cacheKey = CacheKeyFactory.Build(request);
        var cached = await _cache.GetAsync(cacheKey, cancellationToken);

        if (cached is not null)
        {
            return cached with
            {
                Source = "Cache"
            };
        }

        try
        {
            var skuIds = request.Items
                .Select(x => x.SkuId)
                .Distinct()
                .ToList();

            var productsTask = _productCatalogClient.GetProductsAsync(
                skuIds,
                cancellationToken);

            var inventoryTask = _inventoryClient.GetAvailabilityAsync(
                request.SellerId,
                skuIds,
                cancellationToken);

            await Task.WhenAll(productsTask, inventoryTask);

            var products = await productsTask;
            var inventory = await inventoryTask;

            if (products.Count != skuIds.Count)
            {
                return Unavailable("Product information unavailable");
            }

            if (products.Any(x => x.IsRestricted))
            {
                return Unavailable("Restricted item");
            }

            var package = _packageCalculator.Calculate(request, products);

            var fulfillmentCenters = await _fulfillmentClient.GetCandidatesAsync(
                request.SellerId,
                request.Destination,
                package,
                cancellationToken);

            var candidates = await BuildCandidatesAsync(
                request,
                package,
                products,
                inventory,
                fulfillmentCenters,
                cancellationToken);

            if (candidates.Count == 0)
            {
                return Unavailable("No route or inventory available");
            }

            var bestCandidate = _decisionEngine.SelectBest(candidates);
            var response = ToResponse(new ShippingPromise(bestCandidate));

            await _cache.SetAsync(cacheKey, response, PromiseCacheTtl, cancellationToken);
            await _auditRepository.SaveAsync(request, response, candidates, cancellationToken);
            await _eventPublisher.PublishCalculatedAsync(request, response, correlationId, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate shipping promise");

            var fallback = _fallbackEngine.TryBuildFallback(request, ex);

            if (fallback is null)
            {
                return Unavailable("Shipping promise temporarily unavailable");
            }

            var response = ToResponse(new ShippingPromise(fallback));
            await _auditRepository.SaveAsync(request, response, new[] { fallback }, cancellationToken);
            await _eventPublisher.PublishCalculatedAsync(request, response, correlationId, cancellationToken);

            return response;
        }
    }

    private async Task<IReadOnlyList<DeliveryCandidate>> BuildCandidatesAsync(
        ShippingPromiseRequest request,
        PackageData package,
        IReadOnlyList<ProductPhysicalInfo> products,
        IReadOnlyList<InventoryAvailability> inventory,
        IReadOnlyList<FulfillmentCandidate> fulfillmentCenters,
        CancellationToken cancellationToken)
    {
        var candidates = new List<DeliveryCandidate>();

        foreach (var fc in fulfillmentCenters.Where(x => x.HasCapacity))
        {
            var hasInventoryForAllItems = request.Items.All(item =>
                inventory.Any(stock =>
                    stock.SkuId == item.SkuId &&
                    stock.FulfillmentCenterId == fc.FulfillmentCenterId &&
                    stock.AvailableQuantity >= item.Quantity));

            if (!hasInventoryForAllItems)
                continue;

            var routes = await _routingClient.GetRoutesAsync(
                fc.FulfillmentCenterId,
                request.Destination,
                package,
                cancellationToken);

            foreach (var route in routes.Where(x => x.Available))
            {
                var carrierAvailable = await _carrierClient.IsCarrierAvailableAsync(
                    route,
                    request.Destination,
                    package,
                    cancellationToken);

                if (!carrierAvailable)
                    continue;

                var mode = ResolveMode(route);

                var price = await _pricingClient.GetPriceAsync(
                    request,
                    mode,
                    route,
                    package,
                    ResolvePackageCategory(products),
                    cancellationToken);

                var estimatedDate = CalculateEstimatedDeliveryDate(
                    fc.CutoffTime,
                    route.TransitDays);

                candidates.Add(new DeliveryCandidate(
                    Mode: mode,
                    OriginFulfillmentCenterId: fc.FulfillmentCenterId,
                    Carrier: route.CarrierCode,
                    EstimatedDeliveryDate: estimatedDate,
                    ShippingCost: price.Cost - (price.Discount ?? 0),
                    Priority: route.Priority + fc.CapacityScore,
                    IsFallback: false
                ));
            }
        }

        return candidates;
    }

    private static string ResolvePackageCategory(IReadOnlyList<ProductPhysicalInfo> products)
    {
        var categories = products
            .Select(product => product.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return categories.Count == 1 ? categories[0] : "mixed";
    }

    private static ShippingPromiseResponse ToResponse(ShippingPromise promise)
    {
        return new ShippingPromiseResponse(
            Available: true,
            PromiseId: promise.PromiseId,
            Mode: promise.Mode.ToString().ToUpperInvariant(),
            Carrier: promise.Carrier,
            EstimatedDeliveryDate: promise.EstimatedDeliveryDate,
            Cost: promise.Cost,
            Source: promise.IsFallback ? "Fallback" : "Calculated",
            UnavailableReason: null
        );
    }

    private static ShippingMode ResolveMode(RouteOption route)
    {
        if (string.Equals(route.CarrierCode, "MELI_LOGISTICS", StringComparison.OrdinalIgnoreCase))
            return ShippingMode.Fulfillment;

        return ShippingMode.Carrier;
    }

    private static DateOnly CalculateEstimatedDeliveryDate(
        TimeOnly cutoffTime,
        int transitDays)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        var startDate = currentTime > cutoffTime
            ? today.AddDays(1)
            : today;

        return startDate.AddDays(transitDays);
    }

    private static ShippingPromiseResponse Unavailable(string reason)
    {
        return new ShippingPromiseResponse(
            Available: false,
            PromiseId: null,
            Mode: null,
            Carrier: null,
            EstimatedDeliveryDate: null,
            Cost: null,
            Source: "Calculated",
            UnavailableReason: reason
        );
    }

    private static void Validate(ShippingPromiseRequest request)
    {
        if (request is null)
            throw new ArgumentException("Request is required");

        if (request.BuyerId == Guid.Empty)
            throw new ArgumentException("BuyerId is required");

        if (request.SellerId == Guid.Empty)
            throw new ArgumentException("SellerId is required");

        if (request.Destination is null)
            throw new ArgumentException("Destination is required");

        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("At least one item is required");

        if (string.IsNullOrWhiteSpace(request.Destination.ZipCode))
            throw new ArgumentException("Destination zipcode is required");

        if (request.Items.Any(x => x.Quantity <= 0))
            throw new ArgumentException("Item quantity must be greater than zero");
    }
}
