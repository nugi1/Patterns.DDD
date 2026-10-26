namespace Patterns.DDD;

/// <summary>An expected failure: a stable machine-readable code and a human message.</summary>
/// <param name="Code">Stable identifier callers can branch on, e.g. "order-already-paid" or "bad-txnmrklroot".</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Stable identifier callers can branch on. Never empty.</summary>
    public string Code { get; } = RequireCode(Code);

    /// <summary>Human-readable explanation.</summary>
    public string Message { get; } = Message ?? throw new ArgumentNullException(nameof(Message));

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";

    private static string RequireCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(Code));
        return code;
    }
}
