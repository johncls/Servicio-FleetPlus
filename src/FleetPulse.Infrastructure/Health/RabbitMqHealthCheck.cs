using FleetPulse.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FleetPulse.Infrastructure.Health;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConnection _connection;

    public RabbitMqHealthCheck(RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connection.GetAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ is unavailable.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is unavailable.");
        }
    }
}
