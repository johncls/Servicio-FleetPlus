namespace FleetPulse.Application;

public sealed class BatchIngestResult
{
    private BatchIngestResult(int acceptedCount, IReadOnlyList<string> errors)
    {
        AcceptedCount = acceptedCount;
        Errors = errors;
    }

    public bool IsAccepted => Errors.Count == 0;

    public int AcceptedCount { get; }

    public IReadOnlyList<string> Errors { get; }

    public static BatchIngestResult Accepted(int count) => new(count, []);

    public static BatchIngestResult Rejected(IReadOnlyList<string> errors) => new(0, errors);
}
