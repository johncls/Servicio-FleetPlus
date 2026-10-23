using FleetPulse.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FleetPulse.Infrastructure.Messaging;

public sealed class TelemetryConsumerHostedService : BackgroundService
{
    private readonly RabbitMqConnection _connection;
    private readonly TelemetryMessageProcessor _processor;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<TelemetryConsumerHostedService> _logger;

    public TelemetryConsumerHostedService(
        RabbitMqConnection connection,
        TelemetryMessageProcessor processor,
        IOptions<RabbitMqOptions> options,
        ILogger<TelemetryConsumerHostedService> logger)
    {
        _connection = connection;
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
                delay = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Telemetry consumer disconnected. Retrying in {DelaySeconds} seconds.",
                    delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var connection = await _connection.GetAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                consumerDispatchConcurrency: 1),
            stoppingToken);

        await TelemetryTopology.DeclareAsync(channel, _options, stoppingToken);
        await channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var attempt = TelemetryHeaders.ReadAttempt(delivery.BasicProperties);
            try
            {
                var action = await _processor.ProcessAsync(delivery.Body, attempt, stoppingToken);
                switch (action)
                {
                    case DeliveryAction.Acknowledge:
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        break;
                    case DeliveryAction.Retry:
                        await PublishAsync(
                            channel,
                            exchange: "",
                            routingKey: _options.RetryQueue,
                            delivery.Body,
                            attempt + 1,
                            expire: true,
                            stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        break;
                    case DeliveryAction.DeadLetter:
                        await PublishAsync(
                            channel,
                            exchange: "",
                            routingKey: _options.DeadLetterQueue,
                            delivery.Body,
                            attempt,
                            expire: false,
                            stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        break;
                }

                _logger.LogInformation(
                    "Telemetry delivery {DeliveryTag} finished with {Action} on attempt {Attempt}.",
                    delivery.DeliveryTag,
                    action,
                    attempt);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Telemetry delivery {DeliveryTag} failed before acknowledgement.",
                    delivery.DeliveryTag);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, requeue: false, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(_options.Queue, autoAck: false, consumer, stoppingToken);

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdownAsync += (_, _) =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await Task.WhenAny(closed.Task, Task.Delay(Timeout.Infinite, stoppingToken));
        stoppingToken.ThrowIfCancellationRequested();
        throw new IOException("RabbitMQ connection closed.");
    }

    private static Task PublishAsync(
        IChannel channel,
        string exchange,
        string routingKey,
        ReadOnlyMemory<byte> body,
        int attempt,
        bool expire,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>
            {
                [TelemetryHeaders.Attempt] = attempt
            }
        };

        if (expire)
            properties.Expiration = TelemetryHeaders.RetryDelayMilliseconds(attempt - 1).ToString();

        return channel.BasicPublishAsync(
            exchange,
            routingKey,
            mandatory: true,
            properties,
            body,
            cancellationToken).AsTask();
    }
}
