using FleetPulse.Application;

namespace FleetPulse.Application.Tests;

public sealed class FleetQueryLimitsTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validates_vehicle_limit(int limit, bool expected)
    {
        Assert.Equal(expected, FleetQueryLimits.IsValidVehicleLimit(limit));
    }

    [Fact]
    public void Rejects_a_range_that_does_not_move_forward()
    {
        var instant = new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero);

        Assert.False(FleetQueryLimits.IsValidRange(instant, instant));
        Assert.True(FleetQueryLimits.IsValidRange(instant.AddHours(-1), instant));
    }

    [Fact]
    public void Distinguishes_a_missing_vehicle_from_an_empty_track()
    {
        var missing = VehicleTrack.Missing();
        var empty = VehicleTrack.Found([]);

        Assert.False(missing.Exists);
        Assert.True(empty.Exists);
        Assert.Empty(empty.Positions);
    }
}
