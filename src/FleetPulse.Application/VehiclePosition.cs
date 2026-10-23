namespace FleetPulse.Application;

public sealed record VehiclePosition(
    Guid EventId,
    DateTimeOffset RecordedAt,
    double Latitude,
    double Longitude,
    long SequenceNumber);
