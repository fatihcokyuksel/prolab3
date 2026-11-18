namespace StajEvrak.Application.Common;

public record OperationResult(bool Succeeded, string? Error)
{
    public static OperationResult Ok() => new(true, null);
    public static OperationResult Fail(string error) => new(false, error);
}

public sealed record OperationResult<T>(bool Succeeded, string? Error, T? Value) : OperationResult(Succeeded, Error)
{
    public static OperationResult<T> Ok(T value) => new(true, null, value);
    public static new OperationResult<T> Fail(string error) => new(false, error, default);
}
