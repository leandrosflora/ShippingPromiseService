namespace ShippingPromiseService.Infrastructure.Messaging;

public sealed record KafkaEventEnvelope<TPayload>(
    Guid EventId,
    string EventType,
    string SchemaVersion,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string Producer,
    TPayload Payload);
