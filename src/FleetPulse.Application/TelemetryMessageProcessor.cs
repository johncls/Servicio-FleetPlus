using System.Text.Json;
using FleetPulse.Domain;

namespace FleetPulse.Application;

public sealed class TelemetryMessageProcessor
{
    private readonly ITelemetryEventStore _store;
    private readonly TimeProvider _clock;
    private readonly int _maxAttempts;

    public TelemetryMessageProcessor(ITelemetryEventStore store, TimeProvider clock, int maxAttempts)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        _store = store;
        _clock = clock;
        _maxAttempts = maxAttempts;
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
            await _store.IngestAsync(created.Value, cancellationToken);
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
}
