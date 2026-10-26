using System.Collections.Concurrent;
using System.Reflection;

namespace Patterns.DDD.Application;

/// <summary>
/// The stable wire name of a message, e.g. "minibtc.block-connected.v1". It must not change when the
/// class is renamed or moved; that is why the CLR type name is never used.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class MessageTypeAttribute : Attribute
{
    /// <summary>Declares the wire name.</summary>
    public MessageTypeAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The wire name.</summary>
    public string Name { get; }
}

/// <summary>Resolves wire names from <see cref="MessageTypeAttribute"/>.</summary>
public static class MessageTypes
{
    private static readonly ConcurrentDictionary<Type, string> Names = new();

    /// <summary>The wire name of <typeparamref name="T"/>.</summary>
    public static string NameOf<T>() => NameOf(typeof(T));

    /// <summary>The wire name of <paramref name="type"/>.</summary>
    /// <exception cref="InvalidOperationException">The type has no <see cref="MessageTypeAttribute"/>.</exception>
    public static string NameOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Names.GetOrAdd(type, static t =>
            t.GetCustomAttribute<MessageTypeAttribute>(inherit: false)?.Name
            ?? throw new InvalidOperationException(
                $"{t.FullName} has no [MessageType]. Integration events are a published contract; " +
                "give each a stable name, e.g. [MessageType(\"orders.order-paid.v1\")]."));
    }
}
