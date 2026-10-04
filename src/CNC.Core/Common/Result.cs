namespace CNC.Core.Common;

/// <summary>
/// Outcome of an operation that can be rejected for an expected reason (for example, a command
/// refused because the machine is in E-Stop). Exceptions remain reserved for programming errors
/// and unexpected failures.
/// </summary>
public readonly record struct Result
{
    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new Result(false, error);
    }

    public override string ToString() => IsSuccess ? "Success" : $"Failure: {Error}";
}

public readonly record struct Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access the value of a failed result: {Error}");

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new Result<T>(false, default, error);
    }

    public override string ToString() => IsSuccess ? $"Success: {_value}" : $"Failure: {Error}";
}
