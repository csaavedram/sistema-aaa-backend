namespace SistemaAAA.Application.Common;

/// <summary>
/// Resultado genérico usado por los handlers (patrón Result<T>).
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Value { get; private set; }
    public string? Error { get; private set; }
    public string? ErrorCode { get; private set; }

    private Result() { }

    public static Result<T> Success(T value) => new Result<T> { IsSuccess = true, Value = value };

    public static Result<T> Failure(string errorCode, string error)
        => new Result<T> { IsSuccess = false, ErrorCode = errorCode, Error = error };
}
