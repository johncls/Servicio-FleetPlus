using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace FleetPulse.Infrastructure.Health;

public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresHealthCheck(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
    }
}
