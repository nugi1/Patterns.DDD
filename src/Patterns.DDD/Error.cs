namespace Patterns.DDD;

/// <summary>An expected failure: a stable machine-readable code and a human message.</summary>
/// <param name="Code">Stable identifier callers can branch on, e.g. "order-already-paid".</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record Error(string Code, string Message)
{
    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}
