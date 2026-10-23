namespace FleetPulse.Application;

public sealed class PermanentTelemetryException : Exception
{
    public PermanentTelemetryException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class TransientTelemetryException : Exception
{
    public TransientTelemetryException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
