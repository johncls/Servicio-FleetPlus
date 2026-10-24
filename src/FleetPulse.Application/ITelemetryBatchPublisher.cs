using FleetPulse.Domain;

namespace FleetPulse.Application;

public interface ITelemetryBatchPublisher
{
    Task PublishAsync(IReadOnlyList<TelemetryEvent> events, CancellationToken cancellationToken);
}
