using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using ShippingPromiseService.Application;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Infrastructure.Messaging;

public sealed class ShippingQuoteRequestedConsumer : BackgroundService
{
    private const string EventType = "checkout.shipping.quote.requested";
    private const string ConsumerName = "shipping-promise-service";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<ShippingQuoteRequestedConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;

    public ShippingQuoteRequestedConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<ShippingQuoteRequestedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            ClientId = ConsumerName,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topic = _options.Topics.ShippingQuoteRequested;
        _consumer.Subscribe(topic);

        _logger.LogInformation(
            "Kafka consumer subscribed to topic {Topic} with groupId {ConsumerGroupId}",
            topic,
            _options.ConsumerGroupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;

            try
            {
                result = _consumer.Consume(stoppingToken);
                await ProcessAsync(result, stoppingToken);
                _consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (JsonException ex) when (result is not null)
            {
                _logger.LogError(
                    ex,
                    "Invalid Kafka payload on topic {Topic} partition {Partition} offset {Offset}; committing offset to avoid blocking the partition",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value);
                _consumer.Commit(result);
            }
            catch (ArgumentException ex) when (result is not null)
            {
                _logger.LogError(
                    ex,
                    "Invalid Kafka event on topic {Topic} partition {Partition} offset {Offset}; committing offset to avoid blocking the partition",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value);
                _consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Kafka consume failure for topic {Topic}",
                    topic);
            }
            catch (KafkaException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Kafka commit or broker failure for topic {Topic}",
                    topic);
            }
            catch (Exception ex) when (result is not null)
            {
                _logger.LogError(
                    ex,
                    "Failed to process Kafka event on topic {Topic} partition {Partition} offset {Offset}; offset was not committed and the message may be reprocessed",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value);
            }
        }
    }

    private async Task ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<KafkaEventEnvelope<ShippingQuoteRequestedPayload>>(
            result.Message.Value,
            JsonOptions) ?? throw new ArgumentException("Kafka event envelope is required");

        if (!string.Equals(envelope.EventType, EventType, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unexpected eventType '{envelope.EventType}'");
        }

        Validate(envelope.Payload);

        var request = new ShippingPromiseRequest(
            CheckoutId: envelope.Payload.CheckoutId,
            BuyerId: envelope.Payload.BuyerId,
            SellerId: envelope.Payload.SellerId,
            Destination: envelope.Payload.Destination,
            Items: envelope.Payload.Items
                .Select(item => new ShippingPromiseItemDto(
                    SkuId: item.SkuId,
                    Quantity: item.Quantity,
                    UnitPrice: item.UnitPrice))
                .ToList());

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ShippingPromiseApplicationService>();

        await service.CalculateAsync(request, envelope.CorrelationId, cancellationToken);

        _logger.LogInformation(
            "Processed Kafka event {EventType} for checkoutId {CheckoutId} with correlationId {CorrelationId}",
            envelope.EventType,
            envelope.Payload.CheckoutId,
            envelope.CorrelationId);
    }

    private static void Validate(ShippingQuoteRequestedPayload payload)
    {
        if (payload.CheckoutId == Guid.Empty)
            throw new ArgumentException("CheckoutId is required for Kafka shipping quote requests");

        if (payload.BuyerId == Guid.Empty)
            throw new ArgumentException("BuyerId is required");

        if (payload.SellerId == Guid.Empty)
            throw new ArgumentException("SellerId is required");

        if (payload.Destination is null)
            throw new ArgumentException("Destination is required");

        if (payload.Items is null || payload.Items.Count == 0)
            throw new ArgumentException("At least one item is required");

        if (payload.Items.Any(item => item.SkuId == Guid.Empty || item.Quantity <= 0))
            throw new ArgumentException("Items must have skuId and quantity greater than zero");
    }

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        base.Dispose();
    }
}
