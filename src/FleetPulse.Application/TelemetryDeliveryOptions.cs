namespace FleetPulse.Application;

public sealed class TelemetryDeliveryOptions
{
    public const string SectionName = "Telemetry";

    public int MaxAttempts { get; set; } = 5;

    public int DatabaseTimeoutSeconds { get; set; } = 5;
}
