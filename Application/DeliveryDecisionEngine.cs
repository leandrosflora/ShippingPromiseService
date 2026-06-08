using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Application;

public sealed class DeliveryDecisionEngine
{
    public DeliveryCandidate SelectBest(IReadOnlyList<DeliveryCandidate> candidates)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException("No delivery candidates available");

        return candidates
            .OrderBy(x => x.EstimatedDeliveryDate)
            .ThenBy(x => x.ShippingCost)
            .ThenBy(x => x.Priority)
            .First();
    }
}
