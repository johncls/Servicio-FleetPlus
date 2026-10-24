using RabbitMQ.Client;

namespace FleetPulse.Infrastructure.Messaging;

public static class TelemetryTopology
{
    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            options.Exchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        await channel.ExchangeDeclareAsync(
            options.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        var mainQueueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = "telemetry.dlq"
        };

        await channel.QueueDeclareAsync(
            options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArgs,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            options.Queue,
            options.Exchange,
            options.RoutingKey,
            arguments: null,
            noWait: false,
            cancellationToken);

        var retryQueueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = options.Exchange,
            ["x-dead-letter-routing-key"] = options.RoutingKey
        };

        await channel.QueueDeclareAsync(
            options.RetryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryQueueArgs,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            options.DeadLetterQueue,
            options.DeadLetterExchange,
            "telemetry.dlq",
            arguments: null,
            noWait: false,
            cancellationToken);
    }
}
