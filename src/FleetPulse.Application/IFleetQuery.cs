namespace FleetPulse.Application;

public interface IFleetQuery
{
    Task<IReadOnlyList<VehicleState>> ListCurrentAsync(int limit, CancellationToken cancellationToken);

    Task<VehicleTrack> ListPositionsAsync(
        Guid vehicleId,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit,
        CancellationToken cancellationToken);
}
