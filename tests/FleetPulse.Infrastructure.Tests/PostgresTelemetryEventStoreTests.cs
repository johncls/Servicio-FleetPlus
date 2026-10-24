using FleetPulse.Application;
using FleetPulse.Domain;
using FleetPulse.Infrastructure.Postgres;
using Npgsql;
using NpgsqlTypes;

namespace FleetPulse.Infrastructure.Tests;

public sealed class PostgresTelemetryEventStoreTests
{
    [Fact]
    public async Task Keeps_the_newer_position_when_an_older_event_arrives_later()
    {
        var connectionString = ReadConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new PostgresTelemetryEventStore(dataSource, PostgresResilience.Create(TimeSpan.FromSeconds(5)));
        var vehicleId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var olderId = Guid.NewGuid();
        var clock = TimeProvider.System;
        var newer = TelemetryEvent.Create(newerId, vehicleId, clock.GetUtcNow().AddMinutes(-1), 4.65, -74.05, 2, clock).Value;
        var older = TelemetryEvent.Create(olderId, vehicleId, clock.GetUtcNow().AddMinutes(-30), 1.0, -70.0, 1, clock).Value;

        try
        {
            Assert.Equal(IngestStatus.Inserted, await store.IngestAsync(newer, CancellationToken.None));
            Assert.Equal(IngestStatus.Inserted, await store.IngestAsync(older, CancellationToken.None));
            Assert.Equal(IngestStatus.Duplicate, await store.IngestAsync(newer, CancellationToken.None));

            await using var command = dataSource.CreateCommand(
                """
                SELECT last_sequence, latitude,
                       (SELECT count(*) FROM telemetry.vehicle_positions WHERE vehicle_id = @vehicle_id)
                FROM fleet.vehicle_state
                WHERE vehicle_id = @vehicle_id
                """);
            command.Parameters.Add(new NpgsqlParameter("vehicle_id", NpgsqlDbType.Uuid) { Value = vehicleId });
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(2, reader.GetInt64(0));
            Assert.Equal(4.65, reader.GetDouble(1));
            Assert.Equal(2, reader.GetInt64(2));
        }
        finally
        {
            await using var cleanup = dataSource.CreateCommand(
                """
                DELETE FROM fleet.vehicle_state WHERE vehicle_id = @vehicle_id;
                DELETE FROM telemetry.vehicle_positions WHERE vehicle_id = @vehicle_id;
                DELETE FROM telemetry.processed_events WHERE vehicle_id = @vehicle_id;
                """);
            cleanup.Parameters.Add(new NpgsqlParameter("vehicle_id", NpgsqlDbType.Uuid) { Value = vehicleId });
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static string ReadConnectionString()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ".env.example")))
            directory = directory.Parent;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (directory is not null && File.Exists(Path.Combine(directory.FullName, ".env")))
        {
            foreach (var line in File.ReadAllLines(Path.Combine(directory.FullName, ".env")))
            {
                var trimmed = line.Trim();
                var separator = trimmed.IndexOf('=');
                if (separator <= 0 || trimmed.StartsWith('#'))
                    continue;

                values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
            }
        }

        var host = "localhost";
        var port = values.GetValueOrDefault("POSTGRES_PORT", "5433");
        var database = values.GetValueOrDefault("POSTGRES_DB", "fleetplus");
        var user = values.GetValueOrDefault("POSTGRES_USER", "fleetplus");
        var password = values.GetValueOrDefault("POSTGRES_PASSWORD", "change-me");
        return $"Host={host};Port={port};Database={database};Username={user};Password={password};Timeout=5;Command Timeout=5";
    }
}
