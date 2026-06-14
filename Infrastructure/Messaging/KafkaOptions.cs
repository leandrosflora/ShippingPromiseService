using System.ComponentModel.DataAnnotations;

namespace ShippingPromiseService.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required]
    public string BootstrapServers { get; init; } = "localhost:9092";

    [Required]
    public string ConsumerGroupId { get; init; } = "shipping-promise-service";

    public KafkaTopicsOptions Topics { get; init; } = new();
}

public sealed class KafkaTopicsOptions
{
    [Required]
    public string ShippingPromiseCalculated { get; init; } = "shipping.promise.calculated";
}
