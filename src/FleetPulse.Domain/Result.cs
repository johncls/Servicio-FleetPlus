namespace FleetPulse.Domain;

public sealed class Result<T>
{
    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    private Result(string error)
    {
        IsSuccess = false;
        Error = error;
        Value = default!;
    }

    public bool IsSuccess { get; }

    public T Value { get; }

    public string? Error { get; }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string error) => new(error);
}
