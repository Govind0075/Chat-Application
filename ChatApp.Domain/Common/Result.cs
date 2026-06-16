namespace ChatApp.Domain.Common;

/// <summary>
/// Railway-oriented result wrapper — avoids throwing exceptions for expected
/// failures (duplicate email, wrong password, expired token, etc.).
/// Usage:
///   return Result&lt;T&gt;.Success(value);
///   return Result&lt;T&gt;.Failure("Email already in use.");
/// </summary>
public class Result<T>
{
    public bool    IsSuccess { get; private set; }
    public T?      Value     { get; private set; }
    public string? Error     { get; private set; }

    private Result() { }

    public static Result<T> Success(T value) =>
        new() { IsSuccess = true, Value = value };

    public static Result<T> Failure(string error) =>
        new() { IsSuccess = false, Error = error };
}
