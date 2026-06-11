namespace ChatApp.Domain.Common;

public class Result
{
    public bool    IsSuccess { get; }
    public string? Error     { get; }
    protected Result(bool ok, string? err) { IsSuccess = ok; Error = err; }
    public static Result Ok()             => new(true,  null);
    public static Result Fail(string err) => new(false, err);
}

public class Result<T> : Result
{
    public T? Value { get; }
    private Result(bool ok, T? value, string? err) : base(ok, err) { Value = value; }
    public static Result<T> Ok(T value)    => new(true,  value, null);
    public static new Result<T> Fail(string err) => new(false, default, err);
}
