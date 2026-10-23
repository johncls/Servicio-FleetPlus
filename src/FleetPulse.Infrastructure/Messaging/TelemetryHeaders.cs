using System.Globalization;
using System.Text;
using RabbitMQ.Client;

namespace FleetPulse.Infrastructure.Messaging;

internal static class TelemetryHeaders
{
    public const string Attempt = "x-attempt";

    public static int ReadAttempt(IReadOnlyBasicProperties properties)
    {
        if (!properties.IsHeadersPresent() || properties.Headers is null)
            return 1;

        if (!properties.Headers.TryGetValue(Attempt, out var value) || value is null)
            return 1;

        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number => (int)number,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 1
        };
    }

    public static int RetryDelayMilliseconds(int completedAttempt)
    {
        var exponent = Math.Clamp(completedAttempt - 1, 0, 5);
        var delay = 1000 * (1 << exponent);
        return Math.Min(30_000, delay) + Random.Shared.Next(0, 250);
    }
}
