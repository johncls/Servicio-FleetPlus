using FleetPulse.Domain;

namespace FleetPulse.Domain.Tests;

public sealed class TelemetryEventTests
{
    private static readonly TimeProvider Clock =
        new FixedClock(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Accepts_a_valid_event()
    {
        var result = Create(recordedAt: Clock.GetUtcNow(), latitude: 4.65, longitude: -74.05, sequenceNumber: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(4.65, result.Value.Latitude);
        Assert.Equal(DateTimeKind.Utc, result.Value.RecordedAtUtc.Kind);
    }

    [Fact]
    public void Accepts_an_older_timestamp()
    {
        var result = Create(recordedAt: Clock.GetUtcNow().AddHours(-8), latitude: 0, longitude: 0, sequenceNumber: 1);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    [InlineData(double.NaN, 0)]
    public void Rejects_invalid_coordinates(double latitude, double longitude)
    {
        var result = Create(Clock.GetUtcNow(), latitude, longitude, sequenceNumber: 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("Las coordenadas no son válidas.", result.Error);
    }

    [Fact]
    public void Rejects_a_timestamp_more_than_five_minutes_ahead()
    {
        var result = Create(Clock.GetUtcNow().AddMinutes(6), latitude: 0, longitude: 0, sequenceNumber: 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("recordedAt no es válido.", result.Error);
    }

    [Fact]
    public void Rejects_a_negative_sequence()
    {
        var result = Create(Clock.GetUtcNow(), latitude: 0, longitude: 0, sequenceNumber: -1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Rejects_empty_identifiers()
    {
        var result = TelemetryEvent.Create(
            Guid.Empty,
            Guid.NewGuid(),
            Clock.GetUtcNow(),
            0,
            0,
            1,
            Clock);

        Assert.False(result.IsSuccess);
    }

    private static Result<TelemetryEvent> Create(
        DateTimeOffset recordedAt,
        double latitude,
        double longitude,
        long sequenceNumber) =>
        TelemetryEvent.Create(Guid.NewGuid(), Guid.NewGuid(), recordedAt, latitude, longitude, sequenceNumber, Clock);

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedClock(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
