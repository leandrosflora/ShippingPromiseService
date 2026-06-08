namespace ShippingPromiseService.Infrastructure.Persistence;

public sealed class ShippingPromiseAudit
{
    public Guid Id { get; private set; }
    public string RequestJson { get; private set; } = default!;
    public string ResponseJson { get; private set; } = default!;
    public string CandidatesJson { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private ShippingPromiseAudit()
    {
    }

    public ShippingPromiseAudit(
        string requestJson,
        string responseJson,
        string candidatesJson)
    {
        Id = Guid.NewGuid();
        RequestJson = requestJson;
        ResponseJson = responseJson;
        CandidatesJson = candidatesJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
