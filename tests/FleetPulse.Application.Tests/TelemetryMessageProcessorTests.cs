using System.Text.Json;
using FleetPulse.Application;
using FleetPulse.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace FleetPulse.Application.Tests;

public sealed class TelemetryMessageProcessorTests
{
    private static readonly TimeProvider Clock =
        new FixedClock(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Acknowledges_an_inserted_event()
    {
        var store = new FakeStore(IngestStatus.Inserted);
        var processor = Create(store);

        var action = await processor.ProcessAsync(Body(), attempt: 1, CancellationToken.None);

        Assert.Equal(DeliveryAction.Acknowledge, action);
        Assert.Equal(1, store.Calls);
    }

    [Fact]
    public async Task Acknowledges_a_duplicate()
    {
        var store = new FakeStore(IngestStatus.Duplicate);
        var processor = Create(store);

        var action = await processor.ProcessAsync(Body(), attempt: 2, CancellationToken.None);

        Assert.Equal(DeliveryAction.Acknowledge, action);
    }

    [Fact]
    public async Task Dead_letters_an_invalid_payload()
    {
        var store = new FakeStore(IngestStatus.Inserted);
        var processor = Create(store);

        var action = await processor.ProcessAsync("{"u8.ToArray(), attempt: 1, CancellationToken.None);

        Assert.Equal(DeliveryAction.DeadLetter, action);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Retries_a_transient_failure_before_the_last_attempt()
    {
        var store = new FakeStore(new TransientTelemetryException("down"));
        var processor = Create(store);

        var action = await processor.ProcessAsync(Body(), attempt: 2, CancellationToken.None);

        Assert.Equal(DeliveryAction.Retry, action);
    }

    [Fact]
    public async Task Dead_letters_a_transient_failure_on_the_last_attempt()
    {
        var store = new FakeStore(new TransientTelemetryException("down"));
        var processor = Create(store);

        var action = await processor.ProcessAsync(Body(), attempt: 3, CancellationToken.None);

        Assert.Equal(DeliveryAction.DeadLetter, action);
    }

    [Fact]
    public async Task Dead_letters_a_permanent_failure()
    {
        var store = new FakeStore(new PermanentTelemetryException("rejected"));
        var processor = Create(store);

        var action = await processor.ProcessAsync(Body(), attempt: 1, CancellationToken.None);

        Assert.Equal(DeliveryAction.DeadLetter, action);
    }

    [Fact]
    public async Task Notifies_only_when_the_event_is_new()
    {
        var inserted = new FakeStore(IngestStatus.Inserted);
        var insertedNotifier = new RecordingNotifier();
        var insertedProcessor = Create(inserted, insertedNotifier);
        await insertedProcessor.ProcessAsync(Body(), attempt: 1, CancellationToken.None);

        var duplicate = new FakeStore(IngestStatus.Duplicate);
        var duplicateNotifier = new RecordingNotifier();
        var duplicateProcessor = Create(duplicate, duplicateNotifier);
        await duplicateProcessor.ProcessAsync(Body(), attempt: 1, CancellationToken.None);

        Assert.Equal(1, insertedNotifier.Calls);
        Assert.Equal(0, duplicateNotifier.Calls);
    }

    [Fact]
    public async Task Acknowledges_when_the_live_update_fails()
    {
        var store = new FakeStore(IngestStatus.Inserted);
        var processor = Create(store, new ThrowingNotifier());

        var action = await processor.ProcessAsync(Body(), attempt: 1, CancellationToken.None);

        Assert.Equal(DeliveryAction.Acknowledge, action);
    }

    private static TelemetryMessageProcessor Create(
        FakeStore store,
        IVehiclePositionNotifier? notifier = null) =>
        new(
            store,
            notifier ?? new RecordingNotifier(),
            Clock,
            maxAttempts: 3,
            NullLogger<TelemetryMessageProcessor>.Instance);

    private static byte[] Body()
    {
        var message = new TelemetryEventMessage
        {
            EventId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            RecordedAt = Clock.GetUtcNow().AddMinutes(-1),
            Latitude = 4.6,
            Longitude = -74.1,
            SequenceNumber = 4
        };
        return JsonSerializer.SerializeToUtf8Bytes(message, TelemetryEventMessage.JsonOptions);
    }

    private sealed class FakeStore : ITelemetryEventStore
    {
        private readonly IngestStatus _status;
        private readonly Exception? _exception;

        public FakeStore(IngestStatus status) => _status = status;

        public FakeStore(Exception exception) => _exception = exception;

        public int Calls { get; private set; }

        public Task<IngestStatus> IngestAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken)
        {
            Calls++;
            if (_exception is not null)
                throw _exception;

            return Task.FromResult(_status);
        }
    }

    private sealed class RecordingNotifier : IVehiclePositionNotifier
    {
        public int Calls { get; private set; }

        public Task NotifyInsertedAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotifier : IVehiclePositionNotifier
    {
        public Task NotifyInsertedAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("hub unavailable");
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedClock(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
