using ShippingPromiseService.Application;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.UnitTests.Application;

public sealed class DeliveryDecisionEngineTests
{
    private readonly DeliveryDecisionEngine _engine = new();

    [Fact]
    public void SelectBest_WithEmptyList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _engine.SelectBest([]));
    }

    [Fact]
    public void SelectBest_WithSingleCandidate_ReturnsThatCandidate()
    {
        var candidate = MakeCandidate(daysFromNow: 3, cost: 10m, priority: 1);

        var result = _engine.SelectBest([candidate]);

        Assert.Equal(candidate, result);
    }

    [Fact]
    public void SelectBest_SelectsCandidateWithEarliestDeliveryDate()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fast = MakeCandidate(date: today.AddDays(2), cost: 50m, priority: 1);
        var slow = MakeCandidate(date: today.AddDays(5), cost: 10m, priority: 1);

        var result = _engine.SelectBest([slow, fast]);

        Assert.Equal(fast, result);
    }

    [Fact]
    public void SelectBest_WhenSameDate_SelectsCheapest()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        var cheap = MakeCandidate(date: date, cost: 10m, priority: 1);
        var expensive = MakeCandidate(date: date, cost: 50m, priority: 1);

        var result = _engine.SelectBest([expensive, cheap]);

        Assert.Equal(cheap, result);
    }

    [Fact]
    public void SelectBest_WhenSameDateAndCost_SelectsLowestPriority()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        var highPriority = MakeCandidate(date: date, cost: 10m, priority: 1);
        var lowPriority = MakeCandidate(date: date, cost: 10m, priority: 5);

        var result = _engine.SelectBest([lowPriority, highPriority]);

        Assert.Equal(highPriority, result);
    }

    [Fact]
    public void SelectBest_WithMultipleCandidates_ReturnsEarliestAndCheapest()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var candidates = new[]
        {
            MakeCandidate(date: today.AddDays(5), cost: 10m, priority: 1),
            MakeCandidate(date: today.AddDays(2), cost: 30m, priority: 1),
            MakeCandidate(date: today.AddDays(2), cost: 15m, priority: 2),
            MakeCandidate(date: today.AddDays(3), cost: 5m, priority: 1)
        };

        var result = _engine.SelectBest(candidates);

        Assert.Equal(today.AddDays(2), result.EstimatedDeliveryDate);
        Assert.Equal(15m, result.ShippingCost);
    }

    private static DeliveryCandidate MakeCandidate(
        DateOnly? date = null,
        decimal cost = 10m,
        int priority = 1,
        int daysFromNow = 3) =>
        new(
            Mode: ShippingMode.Carrier,
            OriginFulfillmentCenterId: Guid.NewGuid(),
            Carrier: "CARRIER_X",
            EstimatedDeliveryDate: date ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromNow),
            ShippingCost: cost,
            Priority: priority,
            IsFallback: false
        );
}
