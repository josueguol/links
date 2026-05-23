namespace Links.Api.Common;

public readonly record struct Error(string Code, string Message)
{
    public static implicit operator Error((string Code, string Message) tuple)
        => new(tuple.Code, tuple.Message);
}

public sealed class Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }

    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    private Result(T value) => Value = value;
    private Result(Error error) => Error = error;

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}
