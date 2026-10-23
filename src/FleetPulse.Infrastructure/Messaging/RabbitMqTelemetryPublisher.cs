using System.Text.Json;
using FleetPulse.Application;
using FleetPulse.Domain;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FleetPulse.Infrastructure.Messaging;

public sealed class RabbitMqTelemetryPublisher : ITelemetryBatchPublisher
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;

    public RabbitMqTelemetryPublisher(RabbitMqConnection connection, IOptions<RabbitMqOptions> options)
    {
        _connection = connection;
        _options = options.Value;
    }

    public async Task PublishAsync(IReadOnlyList<TelemetryEvent> events, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _connection.GetAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            await TelemetryTopology.DeclareAsync(channel, _options, cancellationToken);

            foreach (var telemetryEvent in events)
            {
                var body = JsonSerializer.SerializeToUtf8Bytes(
                    TelemetryEventMessage.From(telemetryEvent),
                    TelemetryEventMessage.JsonOptions);

                await channel.BasicPublishAsync(
                    _options.Exchange,
                    _options.RoutingKey,
                    mandatory: true,
                    new BasicProperties
                    {
                        Persistent = true,
                        ContentType = "application/json",
                        Headers = new Dictionary<string, object?>
                        {
                            [TelemetryHeaders.Attempt] = 1
                        }
                    },
                    body,
                    cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new TransientTelemetryException("Telemetry broker is unavailable.", exception);
        }
    }
}
