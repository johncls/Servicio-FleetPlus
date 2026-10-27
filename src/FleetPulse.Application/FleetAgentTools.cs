namespace FleetPulse.Application;

public sealed class FleetAgentTools
{
    private readonly IFleetQuery _fleetQuery;
    private readonly TimeProvider _clock;

    public FleetAgentTools(IFleetQuery fleetQuery, TimeProvider clock)
    {
        _fleetQuery = fleetQuery;
        _clock = clock;
    }

    public async Task<IReadOnlyList<VehicleState>> ListVehiclesAsync(int limit, CancellationToken cancellationToken)
    {
        if (!FleetQueryLimits.IsValidVehicleLimit(limit))
            throw new ArgumentOutOfRangeException(nameof(limit));

        return await _fleetQuery.ListCurrentAsync(limit, cancellationToken);
    }

    public async Task<VehicleTrack> GetTelemetryAsync(
        Guid vehicleId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty)
            throw new ArgumentException("vehicleId es obligatorio.", nameof(vehicleId));

        if (!FleetQueryLimits.IsValidPositionLimit(limit))
            throw new ArgumentOutOfRangeException(nameof(limit));

        var to = _clock.GetUtcNow();
        var from = to.AddHours(-24);
        return await _fleetQuery.ListPositionsAsync(vehicleId, from, to, limit, cancellationToken);
    }

    public IncidentReport GetIncidents() => IncidentReport.RegistryUnavailable();
}
