using Microsoft.Extensions.Logging;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Application.Ports;

public sealed class NoOpShippingPromiseEventPublisher : IShippingPromiseEventPublisher
{
    private readonly ILogger<NoOpShippingPromiseEventPublisher> _logger;

    public NoOpShippingPromiseEventPublisher(ILogger<NoOpShippingPromiseEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishCalculatedAsync(
        ShippingPromiseRequest request,
        ShippingPromiseResponse response,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Kafka publisher disabled for eventType {EventType} with correlationId {CorrelationId}",
            "shipping.promise.calculated",
            correlationId);

        return Task.CompletedTask;
    }
}
