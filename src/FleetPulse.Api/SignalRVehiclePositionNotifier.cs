using FleetPulse.Application;
using FleetPulse.Domain;
using Microsoft.AspNetCore.SignalR;

namespace FleetPulse.Api;

public sealed class SignalRVehiclePositionNotifier : IVehiclePositionNotifier
{
    private readonly IHubContext<TelemetryHub> _hub;

    public SignalRVehiclePositionNotifier(IHubContext<TelemetryHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyInsertedAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken)
    {
        return _hub.Clients.All.SendAsync(
            "positionUpdated",
            VehiclePositionUpdated.From(telemetryEvent),
            cancellationToken);
    }
}
