using FleetPulse.Application;
using FleetPulse.Infrastructure.Health;
using FleetPulse.Infrastructure.Messaging;
using FleetPulse.Infrastructure.Postgres;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;

namespace FleetPulse.Api;

public static class FleetPulseServices
{
    public static IServiceCollection AddFleetPulse(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddOptions<TelemetryApiOptions>()
            .BindConfiguration(TelemetryApiOptions.SectionName)
            .PostConfigure<IConfiguration>((options, config) =>
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                    options.ApiKey = config["TELEMETRY_API_KEY"] ?? "";
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "Telemetry:ApiKey is required.")
            .ValidateOnStart();

        services.AddOptions<TelemetryDeliveryOptions>()
            .BindConfiguration(TelemetryDeliveryOptions.SectionName)
            .Validate(options => options.MaxAttempts is >= 1 and <= 10, "Telemetry:MaxAttempts must be between 1 and 10.")
            .Validate(options => options.DatabaseTimeoutSeconds is >= 1 and <= 30, "Telemetry:DatabaseTimeoutSeconds must be between 1 and 30.")
            .ValidateOnStart();

        services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName)
            .PostConfigure<IConfiguration>((options, config) =>
            {
                if (config["RABBITMQ_HOST"] is { Length: > 0 } host && options.Host == "localhost")
                    options.Host = host;

                if (int.TryParse(config["RABBITMQ_PORT"], out var port) && options.Port == 5672 && port > 0)
                    options.Port = port;

                if (config["RABBITMQ_USER"] is { Length: > 0 } user)
                    options.User = user;

                if (string.IsNullOrWhiteSpace(options.Password))
                    options.Password = config["RABBITMQ_PASSWORD"] ?? "";
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMQ host is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMQ password is required.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var connectionString = FleetPlusConnectionString.Resolve(serviceProvider.GetRequiredService<IConfiguration>());
            return NpgsqlDataSource.Create(connectionString);
        });

        services.AddSingleton(serviceProvider =>
        {
            var timeout = serviceProvider.GetRequiredService<IOptions<TelemetryDeliveryOptions>>().Value.DatabaseTimeoutSeconds;
            return PostgresResilience.Create(TimeSpan.FromSeconds(timeout));
        });

        services.AddSignalR();
        services.AddSingleton<ITelemetryEventStore, PostgresTelemetryEventStore>();
        services.AddSingleton<IFleetQuery, PostgresFleetQuery>();
        services.AddSingleton<IVehiclePositionNotifier, SignalRVehiclePositionNotifier>();
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<ITelemetryBatchPublisher, RabbitMqTelemetryPublisher>();
        services.AddSingleton(serviceProvider =>
        {
            var delivery = serviceProvider.GetRequiredService<IOptions<TelemetryDeliveryOptions>>().Value;
            return new TelemetryMessageProcessor(
                serviceProvider.GetRequiredService<ITelemetryEventStore>(),
                serviceProvider.GetRequiredService<IVehiclePositionNotifier>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                delivery.MaxAttempts,
                serviceProvider.GetRequiredService<ILogger<TelemetryMessageProcessor>>());
        });
        services.AddSingleton<IngestTelemetryBatchHandler>();
        services.AddHostedService<TelemetryConsumerHostedService>();

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }
}
