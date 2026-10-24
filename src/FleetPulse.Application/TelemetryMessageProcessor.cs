using System.Text.Json;
using FleetPulse.Domain;
using Microsoft.Extensions.Logging;

namespace FleetPulse.Application;

public sealed class TelemetryMessageProcessor
{
    private readonly ITelemetryEventStore _store;
    private readonly IVehiclePositionNotifier _notifier;
    private readonly TimeProvider _clock;
    private readonly int _maxAttempts;
    private readonly ILogger<TelemetryMessageProcessor> _logger;

    public TelemetryMessageProcessor(
        ITelemetryEventStore store,
        IVehiclePositionNotifier notifier,
        TimeProvider clock,
        int maxAttempts,
        ILogger<TelemetryMessageProcessor> logger)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        _store = store;
        _notifier = notifier;
        _clock = clock;
        _maxAttempts = maxAttempts;
        _logger = logger;
    }

    public async Task<DeliveryAction> ProcessAsync(
        ReadOnlyMemory<byte> body,
        int attempt,
        CancellationToken cancellationToken)
    {
        TelemetryEventMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<TelemetryEventMessage>(body.Span, TelemetryEventMessage.JsonOptions);
        }
        catch (JsonException)
        {
            return DeliveryAction.DeadLetter;
        }

        if (message is null)
            return DeliveryAction.DeadLetter;

        var created = TelemetryEvent.Create(
            message.EventId,
            message.VehicleId,
            message.RecordedAt,
            message.Latitude,
            message.Longitude,
            message.SequenceNumber,
            _clock);

        if (!created.IsSuccess)
            return DeliveryAction.DeadLetter;

        try
        {
            var status = await _store.IngestAsync(created.Value, cancellationToken);
            if (status == IngestStatus.Inserted)
                await NotifyAsync(created.Value, cancellationToken);

            return DeliveryAction.Acknowledge;
        }
        catch (PermanentTelemetryException)
        {
            return DeliveryAction.DeadLetter;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (attempt >= _maxAttempts)
        {
            return DeliveryAction.DeadLetter;
        }
        catch (Exception)
        {
            return DeliveryAction.Retry;
        }
    }

    private async Task NotifyAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.NotifyInsertedAsync(telemetryEvent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Stored telemetry event {EventId} for vehicle {VehicleId}, but the live update was not delivered.",
                telemetryEvent.EventId,
                telemetryEvent.VehicleId);
        }
    }
}
