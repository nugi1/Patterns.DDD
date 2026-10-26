using System.Diagnostics.CodeAnalysis;

namespace Patterns.DDD;

/// <summary>Outcome of an operation that can fail for expected, business reasons.</summary>
/// <remarks>
/// A result is always either <see cref="Result"/> or <see cref="Result{T}"/>: the constructor is
/// not reachable outside this assembly, so no third shape can appear.
/// </remarks>
public class Result
{
    private static readonly Result SuccessInstance = new(null);

    private protected Result(Error? error) => Error = error;

    /// <summary>The failure, or null on success.</summary>
    public Error? Error { get; }

    /// <summary>True when the operation succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>True when the operation failed; <see cref="Error"/> is then set.</summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    /// <summary>A successful result. Shared instance: success carries no state.</summary>
    public static Result Success() => SuccessInstance;

    /// <summary>A failed result.</summary>
    /// <exception cref="ArgumentNullException">A null error would silently mean success.</exception>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    /// <summary>Runs <paramref name="next"/> only if this succeeded; otherwise passes the failure on.</summary>
    public Result Bind(Func<Result> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? next() : this;
    }

    /// <summary>Runs <paramref name="next"/> only if this succeeded; otherwise passes the failure on.</summary>
    public Result<TOut> Bind<TOut>(Func<Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? next() : Result<TOut>.Failure(Error);
    }

    /// <summary>Produces a value on success; a failure passes through unchanged.</summary>
    public Result<TOut> Map<TOut>(Func<TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Result<TOut>.Success(map()) : Result<TOut>.Failure(Error);
    }

    /// <summary>Collapses the result into one value by handling both outcomes.</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess() : onFailure(Error);
    }

    /// <summary>Lets an <see cref="Patterns.DDD.Error"/> be returned where a result is expected.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Outcome that carries a value on success.</summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    // One constructor taking both, so T = Error (or object) can never make overloads ambiguous.
    private Result(T? value, Error? error) : base(error) => _value = value;

    /// <summary>The value; throws if the result failed.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result has no value; it failed with {Error}");

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>A failed result.</summary>
    /// <exception cref="ArgumentNullException">A null error would silently mean success.</exception>
    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }

    /// <summary>Transforms the value on success; a failure passes through unchanged.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Result<TOut>.Success(map(_value!)) : Result<TOut>.Failure(Error);
    }

    /// <summary>Chains a step that can itself fail; stops at the first failure.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? next(_value!) : Result<TOut>.Failure(Error);
    }

    /// <summary>Chains a value-less step that can fail; stops at the first failure.</summary>
    public Result Bind(Func<T, Result> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return IsSuccess ? next(_value!) : this;
    }

    /// <summary>Checks the value against a rule; a broken rule turns this into that rule's failure.</summary>
    public Result<T> Ensure(IRule<T> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (IsFailure)
            return this;

        var check = rule.Check(_value!);
        return check.IsSuccess ? this : Failure(check.Error);
    }

    /// <summary>Collapses the result into one value by handling both outcomes.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(Error);
    }

    /// <summary>Lets a value be returned where a result is expected.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Lets an <see cref="Patterns.DDD.Error"/> be returned where a result is expected.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
}
