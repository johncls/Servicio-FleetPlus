using FleetPulse.Application;
using Npgsql;
using NpgsqlTypes;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace FleetPulse.Infrastructure.Postgres;

public sealed class PostgresFleetQuery : IFleetQuery
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ResiliencePipeline _pipeline;

    public PostgresFleetQuery(NpgsqlDataSource dataSource, ResiliencePipeline pipeline)
    {
        _dataSource = dataSource;
        _pipeline = pipeline;
    }

    public Task<IReadOnlyList<VehicleState>> ListCurrentAsync(int limit, CancellationToken cancellationToken)
    {
        return ExecuteAsync(token => ListCurrentCoreAsync(limit, token), cancellationToken);
    }

    public Task<VehicleTrack> ListPositionsAsync(
        Guid vehicleId,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            token => ListPositionsCoreAsync(vehicleId, from, to, limit, token),
            cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await action(token),
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

    private async Task<IReadOnlyList<VehicleState>> ListCurrentCoreAsync(int limit, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = _dataSource.CreateCommand(
                """
                SELECT vehicle_id, last_event_id, last_sequence, recorded_at, latitude, longitude
                FROM fleet.vehicle_state
                ORDER BY recorded_at DESC
                LIMIT @limit
                """);
            command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = limit });

            var vehicles = new List<VehicleState>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                vehicles.Add(ReadState(reader));

            return vehicles;
        }
        catch (Exception exception) when (PostgresFailureClassifier.IsTransient(exception))
        {
            throw new TransientTelemetryException("Telemetry storage is unavailable.", exception);
        }
    }

    private async Task<VehicleTrack> ListPositionsCoreAsync(
        Guid vehicleId,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var existsCommand = _dataSource.CreateCommand(
                """
                SELECT EXISTS (
                    SELECT 1 FROM fleet.vehicle_state WHERE vehicle_id = @vehicle_id)
                """);
            existsCommand.Parameters.Add(new NpgsqlParameter("vehicle_id", NpgsqlDbType.Uuid) { Value = vehicleId });
            var exists = (bool)(await existsCommand.ExecuteScalarAsync(cancellationToken) ?? false);
            if (!exists)
                return VehicleTrack.Missing();

            await using var command = _dataSource.CreateCommand(
                """
                SELECT event_id, recorded_at, latitude, longitude, sequence_number
                FROM telemetry.vehicle_positions
                WHERE vehicle_id = @vehicle_id
                  AND recorded_at >= @from
                  AND recorded_at < @to
                ORDER BY recorded_at DESC
                LIMIT @limit
                """);
            command.Parameters.Add(new NpgsqlParameter("vehicle_id", NpgsqlDbType.Uuid) { Value = vehicleId });
            command.Parameters.Add(TimestampParameter("from", from));
            command.Parameters.Add(TimestampParameter("to", to));
            command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = limit });

            var positions = new List<VehiclePosition>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                positions.Add(new VehiclePosition(
                    reader.GetGuid(0),
                    ToOffset(reader.GetDateTime(1)),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetInt64(4)));
            }

            return VehicleTrack.Found(positions);
        }
        catch (Exception exception) when (PostgresFailureClassifier.IsTransient(exception))
        {
            throw new TransientTelemetryException("Telemetry storage is unavailable.", exception);
        }
    }

    private static VehicleState ReadState(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt64(2),
            ToOffset(reader.GetDateTime(3)),
            reader.GetDouble(4),
            reader.GetDouble(5));

    private static NpgsqlParameter TimestampParameter(string name, DateTimeOffset value) =>
        new(name, NpgsqlDbType.TimestampTz) { Value = value.UtcDateTime };

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
