namespace HrAuth.Application.Common;

/// <summary>
/// Outcome of an application operation. Carries either a value or the
/// failure reason(s), so layer boundaries never communicate via exceptions.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }

    public T? Value { get; }

    public string Error { get; }

    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, T? value, string error, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        Errors = errors;
    }

    public static Result<T> Success(T value) =>
        new(true, value, string.Empty, []);

    public static Result<T> Failure(string error, IReadOnlyList<string>? errors = null) =>
        new(false, default, error, errors is { Count: > 0 } ? errors : [error]);
}
