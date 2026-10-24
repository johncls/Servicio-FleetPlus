namespace FleetPulse.Application;

public sealed record IncomingTelemetryEvent(
    Guid EventId,
    Guid VehicleId,
    DateTimeOffset RecordedAt,
    double Latitude,
    double Longitude,
    long SequenceNumber);
