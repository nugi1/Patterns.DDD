using System.Security.Cryptography;
using System.Text;

namespace Patterns.DDD.MassTransit;

/// <summary>
/// Deterministic message ids for messages that come from outside (e.g. Bitcoin Core ZMQ notifications),
/// so MassTransit's inbox can deduplicate redeliveries by a natural key.
/// </summary>
/// <example>
/// <code>
/// await publishEndpoint.Publish(new BlockNotified(hash),
///     ctx => ctx.MessageId = MessageIds.FromKey($"block:{hash}"));
/// </code>
/// </example>
public static class MessageIds
{
    // Fixed namespace for Patterns.DDD keys; changing it would change every id.
    private static readonly Guid Namespace = new("6f1c3e0a-5b7d-4c2e-9a8f-2d4b6e8c0a13");

    /// <summary>
    /// The same key always yields the same id: an RFC 9562 version 5 (name-based, SHA-1) UUID.
    /// Prefix keys with their kind ("block:", "tx:") so different kinds never collide.
    /// </summary>
    public static Guid FromKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var name = Encoding.UTF8.GetBytes(key);
        var input = new byte[16 + name.Length];
        Namespace.TryWriteBytes(input, bigEndian: true, out _);
        name.CopyTo(input, 16);

#pragma warning disable CA5350 // SHA-1 is what RFC 9562 v5 prescribes; this is identity, not security.
        var hash = SHA1.HashData(input);
#pragma warning restore CA5350

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // RFC variant
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
