using System.Text.Json;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Infrastructure.Persistence;

public sealed class ShippingPromiseAuditRepository : IShippingPromiseAuditRepository
{
    private readonly ShippingPromiseDbContext _dbContext;

    public ShippingPromiseAuditRepository(ShippingPromiseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveAsync(
        ShippingPromiseRequest request,
        ShippingPromiseResponse response,
        IReadOnlyList<DeliveryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var audit = new ShippingPromiseAudit(
            JsonSerializer.Serialize(request),
            JsonSerializer.Serialize(response),
            JsonSerializer.Serialize(candidates));

        await _dbContext.Audits.AddAsync(audit, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
