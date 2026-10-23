using FleetPulse.Domain;
using Microsoft.Extensions.Logging;

namespace FleetPulse.Application;

public sealed class IngestTelemetryBatchHandler
{
    private readonly ITelemetryBatchPublisher _publisher;
    private readonly TimeProvider _clock;
    private readonly ILogger<IngestTelemetryBatchHandler> _logger;

    public IngestTelemetryBatchHandler(
        ITelemetryBatchPublisher publisher,
        TimeProvider clock,
        ILogger<IngestTelemetryBatchHandler> logger)
    {
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BatchIngestResult> HandleAsync(
        IReadOnlyList<IncomingTelemetryEvent> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
            return BatchIngestResult.Rejected(["El lote está vacío."]);

        if (events.Count > TelemetryBatchLimits.MaxEvents)
        {
            return BatchIngestResult.Rejected(
                [$"El lote admite como máximo {TelemetryBatchLimits.MaxEvents} eventos."]);
        }

        var accepted = new List<TelemetryEvent>(events.Count);
        var errors = new List<string>();
        var seen = new HashSet<Guid>();

        for (var index = 0; index < events.Count; index++)
        {
            var incoming = events[index];
            if (!seen.Add(incoming.EventId))
            {
                errors.Add($"El eventId {incoming.EventId} está repetido en el lote.");
                continue;
            }

            var created = TelemetryEvent.Create(
                incoming.EventId,
                incoming.VehicleId,
                incoming.RecordedAt,
                incoming.Latitude,
                incoming.Longitude,
                incoming.SequenceNumber,
                _clock);

            if (!created.IsSuccess)
            {
                errors.Add($"El evento {incoming.EventId} no es válido: {created.Error}");
                continue;
            }

            accepted.Add(created.Value);
        }

        if (errors.Count > 0)
            return BatchIngestResult.Rejected(errors);

        await _publisher.PublishAsync(accepted, cancellationToken);
        _logger.LogInformation("Accepted telemetry batch of {Count} events.", accepted.Count);
        return BatchIngestResult.Accepted(accepted.Count);
    }
}
