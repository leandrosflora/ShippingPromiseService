using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;
using ShippingPromiseService.Domain;

namespace ShippingPromiseService.Infrastructure.Persistence;

public sealed class ShippingPromiseAuditRepository : IShippingPromiseAuditRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ShippingPromiseDbContext _dbContext;

    public ShippingPromiseAuditRepository(ShippingPromiseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ShippingPromiseResponse?> GetByPromiseIdAsync(
        string promiseId,
        CancellationToken cancellationToken)
    {
        var audits = await _dbContext.Audits
            .AsNoTracking()
            .OrderByDescending(audit => audit.CreatedAt)
            .Select(audit => audit.ResponseJson)
            .ToListAsync(cancellationToken);

        foreach (var responseJson in audits)
        {
            var response = JsonSerializer.Deserialize<ShippingPromiseResponse>(responseJson, JsonOptions);

            if (string.Equals(response?.PromiseId, promiseId, StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }
        }

        return null;
    }

    public async Task SaveAsync(
        ShippingPromiseRequest request,
        ShippingPromiseResponse response,
        IReadOnlyList<DeliveryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var audit = new ShippingPromiseAudit(
            JsonSerializer.Serialize(request, JsonOptions),
            JsonSerializer.Serialize(response, JsonOptions),
            JsonSerializer.Serialize(candidates, JsonOptions));

        await _dbContext.Audits.AddAsync(audit, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
