using FleetPulse.Domain;

namespace FleetPulse.Application;

public interface IVehiclePositionNotifier
{
    Task NotifyInsertedAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken);
}
