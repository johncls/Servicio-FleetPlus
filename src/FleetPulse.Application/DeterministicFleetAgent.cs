using System.Globalization;
using System.Text.RegularExpressions;

namespace FleetPulse.Application;

public sealed partial class DeterministicFleetAgent : IFleetAgent
{
    public const string ModeName = "deterministic";

    private static readonly Regex VehicleIdPattern = VehicleIdRegex();

    private readonly FleetAgentTools _tools;

    public DeterministicFleetAgent(FleetAgentTools tools)
    {
        _tools = tools;
    }

    public async Task<AgentReply> AskAsync(string question, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Trim().Length > 500)
        {
            return Reply(
                "La pregunta tiene que tener entre 1 y 500 caracteres.",
                new { accepted = false });
        }

        var text = question.Trim();
        if (MentionsIncidents(text))
        {
            var report = _tools.GetIncidents();
            return Reply(
                "No hay un registro de incidentes. No puedo informar alertas ni inventarlas.",
                report);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            return await AnswerFromDataAsync(text, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Reply("La consulta superó el tiempo límite. No tengo un resultado.", new { status = "timeout" });
        }
    }

    private async Task<AgentReply> AnswerFromDataAsync(string text, CancellationToken cancellationToken)
    {
        var vehicleMatch = VehicleIdPattern.Match(text);
        if (vehicleMatch.Success && Guid.TryParse(vehicleMatch.Value, out var vehicleId))
        {
            var track = await _tools.GetTelemetryAsync(
                vehicleId,
                FleetQueryLimits.DefaultPositions,
                cancellationToken);

            if (!track.Exists)
            {
                return Reply(
                    $"El vehículo {vehicleId} no está registrado.",
                    new { vehicleId, status = "missing" });
            }

            if (track.Positions.Count == 0)
            {
                return Reply(
                    $"El vehículo {vehicleId} existe, pero no tiene posiciones en las últimas 24 horas.",
                    new { vehicleId, status = "empty", positions = track.Positions });
            }

            return Reply(
                DescribePositions(vehicleId, track.Positions),
                new { vehicleId, status = "found", positions = track.Positions });
        }

        if (MentionsFleet(text))
        {
            var vehicles = await _tools.ListVehiclesAsync(FleetQueryLimits.DefaultVehicles, cancellationToken);
            if (vehicles.Count == 0)
            {
                return Reply(
                    "No hay vehículos con posición registrada.",
                    new { status = "empty", vehicles });
            }

            return Reply(DescribeVehicles(vehicles), new { status = "found", vehicles });
        }

        return Reply(
            "Solo puedo consultar vehículos, su telemetría de las últimas 24 horas y el registro de incidentes. Incluye un vehicleId o pregunta por la flota.",
            new { accepted = false });
    }

    private static AgentReply Reply(string answer, object evidence) =>
        new(ModeName, UsesLanguageModel: false, answer, evidence);

    private static string DescribeVehicles(IReadOnlyList<VehicleState> vehicles)
    {
        var lines = vehicles.Select(vehicle =>
            string.Create(CultureInfo.InvariantCulture, $"{vehicle.VehicleId}: {vehicle.Latitude}, {vehicle.Longitude}, secuencia {vehicle.SequenceNumber}, {vehicle.RecordedAt:O}."));
        return $"Hay {vehicles.Count} vehículo(s) con posición vigente. {string.Join(" ", lines)}";
    }

    private static string DescribePositions(Guid vehicleId, IReadOnlyList<VehiclePosition> positions)
    {
        var latest = positions[0];
        return string.Create(
            CultureInfo.InvariantCulture,
            $"El vehículo {vehicleId} tiene {positions.Count} posición(es) en las últimas 24 horas. La más reciente es {latest.Latitude}, {latest.Longitude}, secuencia {latest.SequenceNumber}, {latest.RecordedAt:O}.");
    }

    private static bool MentionsIncidents(string text)
    {
        var value = text.ToLowerInvariant();
        return value.Contains("incidente") || value.Contains("alerta");
    }

    private static bool MentionsFleet(string text)
    {
        var value = text.ToLowerInvariant();
        return value.Contains("flota")
            || value.Contains("vehiculo")
            || value.Contains("vehículo")
            || value.Contains("posicion")
            || value.Contains("posición")
            || value.Contains("donde")
            || value.Contains("dónde");
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
    private static partial Regex VehicleIdRegex();
}
