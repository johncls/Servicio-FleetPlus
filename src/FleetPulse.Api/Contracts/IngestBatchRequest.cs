namespace FleetPulse.Api.Contracts;

public sealed class AgentQuestionRequest
{
    public string Question { get; init; } = "";
}

public sealed class IngestBatchRequest
{
    public IReadOnlyList<TelemetryEventRequest> Events { get; init; } = [];
}

public sealed class TelemetryEventRequest
{
    public Guid EventId { get; init; }

    public Guid VehicleId { get; init; }

    public DateTimeOffset RecordedAt { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public long SequenceNumber { get; init; }
}
