namespace Tarantula.Application.Common;

public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    TooLarge,
    Unavailable,
    Unexpected
}

/// <summary>A business failure. <see cref="Code"/> is stable and machine readable; <see cref="Message"/> is for logs and developers.</summary>
public sealed record Error(ErrorKind Kind, string Code, string Message);

/// <summary>Outcome of a use case. Expected failures are values, not exceptions.</summary>
public readonly record struct Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }

    public static Result<T> Failure(ErrorKind kind, string code, string message) =>
        Failure(new Error(kind, code, message));
}
