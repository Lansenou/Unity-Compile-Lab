namespace Ucl.Core.Model;

/// <summary>A value or an error message. Parsers return this instead of throwing, so configuration problems stay values.</summary>
/// <typeparam name="T">The parsed value type.</typeparam>
public readonly record struct Result<T>
{
    private Result(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>The value; default when <see cref="Error"/> is set.</summary>
    public T? Value { get; }

    /// <summary>The error message, or null on success.</summary>
    public string? Error { get; }

    /// <summary>True when there is a value.</summary>
    public bool Ok => Error is null;

    /// <summary>Creates a successful result.</summary>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>Creates a failed result.</summary>
    public static Result<T> Failure(string error) => new(default, error);
}
