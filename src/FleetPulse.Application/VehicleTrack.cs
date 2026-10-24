namespace FleetPulse.Application;

public sealed record VehicleTrack(bool Exists, IReadOnlyList<VehiclePosition> Positions)
{
    public static VehicleTrack Missing() => new(false, []);

    public static VehicleTrack Found(IReadOnlyList<VehiclePosition> positions) => new(true, positions);
}
