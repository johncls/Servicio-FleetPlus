using FleetPulse.Application;
using FleetPulse.Domain;
using FleetPulse.Infrastructure.Postgres;
using Npgsql;

namespace FleetPulse.Infrastructure.Tests;

public sealed class PostgresFleetQueryTests
{
    [Fact]
    public async Task Returns_current_state_and_distinguishes_an_empty_range_from_an_unknown_vehicle()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString());
        var pipeline = PostgresResilience.Create(TimeSpan.FromSeconds(5));
        var store = new PostgresTelemetryEventStore(dataSource, pipeline);
        var query = new PostgresFleetQuery(dataSource, pipeline);
        var vehicleId = Guid.NewGuid();
        var clock = TimeProvider.System;
        var recordedAt = clock.GetUtcNow().AddMinutes(-2);
        var created = TelemetryEvent.Create(
            Guid.NewGuid(),
            vehicleId,
            recordedAt,
            4.7,
            -74.1,
            3,
            clock);

        try
        {
            Assert.Equal(IngestStatus.Inserted, await store.IngestAsync(created.Value, CancellationToken.None));

            var current = await query.ListCurrentAsync(100, CancellationToken.None);
            Assert.Contains(current, vehicle => vehicle.VehicleId == vehicleId && vehicle.SequenceNumber == 3);

            var emptyRange = await query.ListPositionsAsync(
                vehicleId,
                recordedAt.AddHours(2),
                recordedAt.AddHours(3),
                100,
                CancellationToken.None);
            Assert.True(emptyRange.Exists);
            Assert.Empty(emptyRange.Positions);

            var missing = await query.ListPositionsAsync(
                Guid.NewGuid(),
                recordedAt.AddHours(-1),
                recordedAt.AddHours(1),
                100,
                CancellationToken.None);
            Assert.False(missing.Exists);
        }
        finally
        {
            await using var cleanup = dataSource.CreateCommand(
                """
                DELETE FROM fleet.vehicle_state WHERE vehicle_id = @vehicle_id;
                DELETE FROM telemetry.vehicle_positions WHERE vehicle_id = @vehicle_id;
                DELETE FROM telemetry.processed_events WHERE vehicle_id = @vehicle_id;
                """);
            cleanup.Parameters.Add(new Npgsql.NpgsqlParameter("vehicle_id", vehicleId));
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static string ConnectionString()
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

        var port = values.GetValueOrDefault("POSTGRES_PORT", "5433");
        var database = values.GetValueOrDefault("POSTGRES_DB", "fleetplus");
        var user = values.GetValueOrDefault("POSTGRES_USER", "fleetplus");
        var password = values.GetValueOrDefault("POSTGRES_PASSWORD", "change-me");
        return $"Host=localhost;Port={port};Database={database};Username={user};Password={password};Timeout=5;Command Timeout=5";
    }
}
