namespace FleetPulse.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string User { get; set; } = "fleetplus";

    public string Password { get; set; } = "";

    public string Exchange { get; set; } = "fleetpulse.telemetry";

    public string Queue { get; set; } = "fleetpulse.telemetry";

    public string RetryQueue { get; set; } = "fleetpulse.telemetry.retry";

    public string DeadLetterExchange { get; set; } = "fleetpulse.telemetry.dlx";

    public string DeadLetterQueue { get; set; } = "fleetpulse.telemetry.dlq";

    public string RoutingKey { get; set; } = "telemetry";
}
