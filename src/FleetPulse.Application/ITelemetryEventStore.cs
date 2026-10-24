using FleetPulse.Domain;

namespace FleetPulse.Application;

public interface ITelemetryEventStore
{
    Task<IngestStatus> IngestAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken);
}
