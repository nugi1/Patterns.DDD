namespace Patterns.DDD;

/// <summary>
/// A broken invariant that indicates a bug in the calling code.
/// Expected failures use <see cref="Result"/> instead.
/// </summary>
/// <param name="message">What invariant was broken.</param>
public sealed class DomainException(string message) : Exception(message);
