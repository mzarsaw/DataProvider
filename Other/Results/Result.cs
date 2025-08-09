namespace Results;

#pragma warning disable CA1034 // Nested types should not be visible

/// <summary>
/// Represents a result that can either be successful with a value or failed with an error
/// </summary>
/// <typeparam name="TValue">The type of the success value</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
public abstract record Result<TValue, TError>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result{TValue, TError}"/> class
    /// </summary>
    protected Result() { }

    /// <summary>
    /// Represents a successful result with a value
    /// </summary>
    /// <param name="Value">The success value</param>


    public sealed record Success(TValue Value) : Result<TValue, TError>;

    /// <summary>
    /// Represents a failed result with an error
    /// </summary>
    /// <param name="ErrorValue">The error value</param>
    public sealed record Failure(TError ErrorValue) : Result<TValue, TError>;
}
