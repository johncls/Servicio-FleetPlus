using FleetPulse.Application;
using FleetPulse.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace FleetPulse.Application.Tests;

public sealed class IngestTelemetryBatchHandlerTests
{
    private static readonly TimeProvider Clock =
        new FixedClock(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Publishes_only_after_the_whole_batch_is_valid()
    {
        var publisher = new FakePublisher();
        var handler = new IngestTelemetryBatchHandler(publisher, Clock, NullLogger<IngestTelemetryBatchHandler>.Instance);
        var events = new[]
        {
            Event(sequence: 2, recordedAt: Clock.GetUtcNow()),
            Event(sequence: 1, recordedAt: Clock.GetUtcNow().AddMinutes(-30))
        };

        var result = await handler.HandleAsync(events, CancellationToken.None);

        Assert.True(result.IsAccepted);
        Assert.Equal(2, publisher.Published.Count);
        Assert.Equal(2, publisher.Published[0].SequenceNumber);
        Assert.Equal(1, publisher.Published[1].SequenceNumber);
    }

    [Fact]
    public async Task Does_not_publish_when_one_event_is_invalid()
    {
        var publisher = new FakePublisher();
        var handler = new IngestTelemetryBatchHandler(publisher, Clock, NullLogger<IngestTelemetryBatchHandler>.Instance);
        var events = new[]
        {
            Event(sequence: 1, recordedAt: Clock.GetUtcNow()),
            Event(sequence: 2, recordedAt: Clock.GetUtcNow(), latitude: 120)
        };

        var result = await handler.HandleAsync(events, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task Rejects_a_duplicate_event_id_inside_the_batch()
    {
        var publisher = new FakePublisher();
        var handler = new IngestTelemetryBatchHandler(publisher, Clock, NullLogger<IngestTelemetryBatchHandler>.Instance);
        var eventId = Guid.NewGuid();
        var events = new[]
        {
            Event(sequence: 1, recordedAt: Clock.GetUtcNow(), eventId: eventId),
            Event(sequence: 2, recordedAt: Clock.GetUtcNow(), eventId: eventId)
        };

        var result = await handler.HandleAsync(events, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task Rejects_an_oversized_batch()
    {
        var publisher = new FakePublisher();
        var handler = new IngestTelemetryBatchHandler(publisher, Clock, NullLogger<IngestTelemetryBatchHandler>.Instance);
        var events = Enumerable.Range(0, TelemetryBatchLimits.MaxEvents + 1)
            .Select(index => Event(index, Clock.GetUtcNow()))
            .ToArray();

        var result = await handler.HandleAsync(events, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Empty(publisher.Published);
    }

    private static IncomingTelemetryEvent Event(
        long sequence,
        DateTimeOffset recordedAt,
        Guid? eventId = null,
        double latitude = 4.6) =>
        new(eventId ?? Guid.NewGuid(), Guid.NewGuid(), recordedAt, latitude, -74.1, sequence);

    private sealed class FakePublisher : ITelemetryBatchPublisher
    {
        public List<TelemetryEvent> Published { get; } = [];

        public Task PublishAsync(IReadOnlyList<TelemetryEvent> events, CancellationToken cancellationToken)
        {
            Published.AddRange(events);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedClock(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
