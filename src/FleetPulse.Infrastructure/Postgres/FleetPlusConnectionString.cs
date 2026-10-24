using Microsoft.Extensions.Configuration;

namespace FleetPulse.Infrastructure.Postgres;

public static class FleetPlusConnectionString
{
    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("FleetPlus");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var host = configuration["POSTGRES_HOST"] ?? "localhost";
        var port = configuration["POSTGRES_PORT"] ?? "5433";
        var database = configuration["POSTGRES_DB"] ?? "fleetplus";
        var user = configuration["POSTGRES_USER"] ?? "fleetplus";
        var password = configuration["POSTGRES_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:FleetPlus or POSTGRES_PASSWORD is required.");
        }

        return $"Host={host};Port={port};Database={database};Username={user};Password={password};Timeout=5;Command Timeout=5";
    }
}
