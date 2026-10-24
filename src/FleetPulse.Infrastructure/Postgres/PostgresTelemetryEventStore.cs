using FleetPulse.Application;
using FleetPulse.Domain;
using Npgsql;
using NpgsqlTypes;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace FleetPulse.Infrastructure.Postgres;

public sealed class PostgresTelemetryEventStore : ITelemetryEventStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ResiliencePipeline _pipeline;

    public PostgresTelemetryEventStore(NpgsqlDataSource dataSource, ResiliencePipeline pipeline)
    {
        _dataSource = dataSource;
        _pipeline = pipeline;
    }

    public async Task<IngestStatus> IngestAsync(
        TelemetryEvent telemetryEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await IngestCoreAsync(telemetryEvent, token),
                cancellationToken);
        }
        catch (BrokenCircuitException exception)
        {
            throw new TransientTelemetryException("Telemetry storage is unavailable.", exception);
        }
        catch (TimeoutRejectedException exception)
        {
            throw new TransientTelemetryException("Telemetry storage is unavailable.", exception);
        }
    }

    private async Task<IngestStatus> IngestCoreAsync(
        TelemetryEvent telemetryEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = _dataSource.CreateCommand(
                """
                SELECT telemetry.ingest_event(
                    @event_id,
                    @vehicle_id,
                    @recorded_at,
                    @latitude,
                    @longitude,
                    @sequence_number)
                """);

            command.Parameters.Add(new NpgsqlParameter("event_id", NpgsqlDbType.Uuid) { Value = telemetryEvent.EventId });
            command.Parameters.Add(new NpgsqlParameter("vehicle_id", NpgsqlDbType.Uuid) { Value = telemetryEvent.VehicleId });
            command.Parameters.Add(new NpgsqlParameter("recorded_at", NpgsqlDbType.TimestampTz)
            {
                Value = DateTime.SpecifyKind(telemetryEvent.RecordedAtUtc, DateTimeKind.Utc)
            });
            command.Parameters.Add(new NpgsqlParameter("latitude", NpgsqlDbType.Double) { Value = telemetryEvent.Latitude });
            command.Parameters.Add(new NpgsqlParameter("longitude", NpgsqlDbType.Double) { Value = telemetryEvent.Longitude });
            command.Parameters.Add(new NpgsqlParameter("sequence_number", NpgsqlDbType.Bigint) { Value = telemetryEvent.SequenceNumber });

            var result = (string?)await command.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                "inserted" => IngestStatus.Inserted,
                "duplicate" => IngestStatus.Duplicate,
                _ => throw new PermanentTelemetryException("Telemetry storage returned an unknown result.")
            };
        }
        catch (Exception exception) when (PostgresFailureClassifier.IsPermanent(exception))
        {
            throw new PermanentTelemetryException("Telemetry event was rejected by storage.", exception);
        }
        catch (Exception exception) when (PostgresFailureClassifier.IsTransient(exception))
        {
            throw new TransientTelemetryException("Telemetry storage is unavailable.", exception);
        }
    }
}
