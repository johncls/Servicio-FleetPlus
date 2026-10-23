using System.Text.Json;
using FleetPulse.Domain;

namespace FleetPulse.Application;

public sealed class TelemetryEventMessage
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid EventId { get; init; }

    public Guid VehicleId { get; init; }

    public DateTimeOffset RecordedAt { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public long SequenceNumber { get; init; }

    public static TelemetryEventMessage From(TelemetryEvent telemetryEvent)
    {
        var recordedAt = new DateTimeOffset(
            DateTime.SpecifyKind(telemetryEvent.RecordedAtUtc, DateTimeKind.Utc));

        return new TelemetryEventMessage
        {
            EventId = telemetryEvent.EventId,
            VehicleId = telemetryEvent.VehicleId,
            RecordedAt = recordedAt,
            Latitude = telemetryEvent.Latitude,
            Longitude = telemetryEvent.Longitude,
            SequenceNumber = telemetryEvent.SequenceNumber
        };
    }
}
