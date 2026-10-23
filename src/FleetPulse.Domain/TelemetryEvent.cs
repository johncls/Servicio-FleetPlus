namespace FleetPulse.Domain;

public sealed class TelemetryEvent
{
    private static readonly DateTimeOffset EarliestRecordedAt =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private TelemetryEvent(
        Guid eventId,
        Guid vehicleId,
        DateTime recordedAtUtc,
        double latitude,
        double longitude,
        long sequenceNumber)
    {
        EventId = eventId;
        VehicleId = vehicleId;
        RecordedAtUtc = recordedAtUtc;
        Latitude = latitude;
        Longitude = longitude;
        SequenceNumber = sequenceNumber;
    }

    public Guid EventId { get; }

    public Guid VehicleId { get; }

    public DateTime RecordedAtUtc { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public long SequenceNumber { get; }

    public static Result<TelemetryEvent> Create(
        Guid eventId,
        Guid vehicleId,
        DateTimeOffset recordedAt,
        double latitude,
        double longitude,
        long sequenceNumber,
        TimeProvider clock)
    {
        if (eventId == Guid.Empty)
            return Result<TelemetryEvent>.Failure("eventId es obligatorio.");

        if (vehicleId == Guid.Empty)
            return Result<TelemetryEvent>.Failure("vehicleId es obligatorio.");

        var recordedAtUtc = recordedAt.ToUniversalTime();
        var latestAccepted = clock.GetUtcNow().AddMinutes(5);
        if (recordedAtUtc < EarliestRecordedAt || recordedAtUtc > latestAccepted)
            return Result<TelemetryEvent>.Failure("recordedAt no es válido.");

        if (!IsValidCoordinate(latitude, -90, 90) || !IsValidCoordinate(longitude, -180, 180))
            return Result<TelemetryEvent>.Failure("Las coordenadas no son válidas.");

        if (sequenceNumber < 0)
            return Result<TelemetryEvent>.Failure("sequenceNumber no es válido.");

        return Result<TelemetryEvent>.Success(new TelemetryEvent(
            eventId,
            vehicleId,
            recordedAtUtc.UtcDateTime,
            latitude,
            longitude,
            sequenceNumber));
    }

    private static bool IsValidCoordinate(double value, double minimum, double maximum) =>
        !double.IsNaN(value)
        && !double.IsInfinity(value)
        && value >= minimum
        && value <= maximum;
}
