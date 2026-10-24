using FleetPulse.Domain;

namespace FleetPulse.Application;

public sealed record VehiclePositionUpdated(
    Guid VehicleId,
    Guid EventId,
    DateTimeOffset RecordedAt,
    double Latitude,
    double Longitude,
    long SequenceNumber)
{
    public static VehiclePositionUpdated From(TelemetryEvent telemetryEvent)
    {
        var recordedAt = new DateTimeOffset(
            DateTime.SpecifyKind(telemetryEvent.RecordedAtUtc, DateTimeKind.Utc));

        return new VehiclePositionUpdated(
            telemetryEvent.VehicleId,
            telemetryEvent.EventId,
            recordedAt,
            telemetryEvent.Latitude,
            telemetryEvent.Longitude,
            telemetryEvent.SequenceNumber);
    }
}
