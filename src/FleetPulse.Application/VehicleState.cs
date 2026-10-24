namespace FleetPulse.Application;

public sealed record VehicleState(
    Guid VehicleId,
    Guid LastEventId,
    long SequenceNumber,
    DateTimeOffset RecordedAt,
    double Latitude,
    double Longitude);
