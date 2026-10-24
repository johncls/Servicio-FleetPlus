namespace FleetPulse.Application;

public sealed record IncidentReport(string Status, string Reason)
{
    public static IncidentReport RegistryUnavailable() =>
        new("unavailable", "No hay un registro de incidentes en el sistema.");
}
