using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Infrastructure.Messaging;

public sealed class KafkaShippingPromiseEventPublisher : IShippingPromiseEventPublisher, IDisposable
{
    private const string EventType = "shipping.promise.calculated";
    private const string ProducerName = "shipping-promise-service";
    private const string SchemaVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaShippingPromiseEventPublisher> _logger;

    public KafkaShippingPromiseEventPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaShippingPromiseEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = ProducerName,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublishCalculatedAsync(
        ShippingPromiseRequest request,
        ShippingPromiseResponse response,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!response.Available || response.PromiseId is null || response.Mode is null || response.Carrier is null ||
            response.EstimatedDeliveryDate is null || response.Cost is null)
        {
            return;
        }

        var topic = _options.Topics.ShippingPromiseCalculated;
        var messageKey = response.PromiseId;
        var envelope = new KafkaEventEnvelope<ShippingPromiseCalculatedPayload>(
            EventId: Guid.NewGuid(),
            EventType: EventType,
            SchemaVersion: SchemaVersion,
            OccurredAt: DateTimeOffset.UtcNow,
            CorrelationId: correlationId,
            Producer: ProducerName,
            Payload: new ShippingPromiseCalculatedPayload(
                BuyerId: request.BuyerId,
                SellerId: request.SellerId,
                Destination: request.Destination,
                Items: request.Items,
                PromiseId: response.PromiseId,
                Mode: response.Mode,
                Carrier: response.Carrier,
                EstimatedDeliveryDate: response.EstimatedDeliveryDate.Value,
                Cost: response.Cost.Value,
                Source: response.Source));

        try
        {
            var message = new Message<string, string>
            {
                Key = messageKey,
                Value = JsonSerializer.Serialize(envelope, JsonOptions),
                Headers = new Headers
                {
                    { "eventType", System.Text.Encoding.UTF8.GetBytes(EventType) },
                    { "correlationId", System.Text.Encoding.UTF8.GetBytes(correlationId) }
                }
            };

            var result = await _producer.ProduceAsync(topic, message, cancellationToken);

            _logger.LogInformation(
                "Published Kafka event to topic {Topic} with message key {MessageKey}, eventType {EventType} and correlationId {CorrelationId} at partition {Partition} offset {Offset}",
                topic,
                messageKey,
                EventType,
                correlationId,
                result.Partition.Value,
                result.Offset.Value);
        }
        catch (Exception ex) when (ex is ProduceException<string, string> or KafkaException or OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish Kafka event to topic {Topic} with message key {MessageKey}, eventType {EventType} and correlationId {CorrelationId}",
                topic,
                messageKey,
                EventType,
                correlationId);
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
