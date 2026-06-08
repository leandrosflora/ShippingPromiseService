namespace ShippingPromiseService.Domain;

public sealed class ShippingPromise
{
    public string PromiseId { get; private set; }
    public ShippingMode Mode { get; private set; }
    public string Carrier { get; private set; }
    public DateOnly EstimatedDeliveryDate { get; private set; }
    public decimal Cost { get; private set; }
    public bool IsFallback { get; private set; }

    private ShippingPromise()
    {
        PromiseId = default!;
        Carrier = default!;
    }

    public ShippingPromise(DeliveryCandidate candidate)
    {
        PromiseId = $"promise_{Guid.NewGuid():N}";
        Mode = candidate.Mode;
        Carrier = candidate.Carrier;
        EstimatedDeliveryDate = candidate.EstimatedDeliveryDate;
        Cost = candidate.ShippingCost;
        IsFallback = candidate.IsFallback;
    }
}
