using System.Diagnostics.CodeAnalysis;

namespace Patterns.DDD;

/// <summary>Outcome of an operation that can fail for expected, business reasons.</summary>
public class Result
{
    /// <summary>Creates a result; a null error means success.</summary>
    protected Result(Error? error) => Error = error;

    /// <summary>The failure, or null on success.</summary>
    public Error? Error { get; }

    /// <summary>True when the operation succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>True when the operation failed; <see cref="Error"/> is then set.</summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    /// <summary>A successful result.</summary>
    public static Result Success() => new(null);

    /// <summary>A failed result.</summary>
    public static Result Failure(Error error) => new(error);

    /// <summary>Lets an <see cref="Patterns.DDD.Error"/> be returned where a result is expected.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Outcome that carries a value on success.</summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    /// <summary>The value; throws if the result failed.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result has no value; it failed with {Error}");

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>A failed result.</summary>
    public static new Result<T> Failure(Error error) => new(default, error);

    /// <summary>Lets a value be returned where a result is expected.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Lets an <see cref="Patterns.DDD.Error"/> be returned where a result is expected.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
}
