namespace FleetPulse.Application;

public static class FleetQueryLimits
{
    public const int DefaultVehicles = 100;

    public const int MaxVehicles = 100;

    public const int DefaultPositions = 100;

    public const int MaxPositions = 500;

    public static bool IsValidVehicleLimit(int limit) => limit is >= 1 and <= MaxVehicles;

    public static bool IsValidPositionLimit(int limit) => limit is >= 1 and <= MaxPositions;

    public static bool IsValidRange(DateTimeOffset from, DateTimeOffset to) => from < to;
}
