using System.Collections.ObjectModel;

namespace Patterns.DDD.Application;

/// <summary>Well-known header names carried with a message.</summary>
public static class MessageHeaders
{
    /// <summary>W3C trace context of the operation that produced the message.</summary>
    public const string TraceParent = "traceparent";

    /// <summary>W3C trace state accompanying <see cref="TraceParent"/>.</summary>
    public const string TraceState = "tracestate";

    /// <summary>Identifier shared by all messages of one business flow.</summary>
    public const string CorrelationId = "correlation-id";
}

/// <summary>
/// Metadata a handler receives with a message. <see cref="MessageId"/> identifies the message, not the
/// delivery: every redelivery of the same message carries the same id, which is what makes
/// deduplication possible.
/// </summary>
public sealed record MessageContext
{
    /// <summary>Creates the context.</summary>
    /// <param name="messageId">Stable id; an outbox row id, or a natural key such as a txid.</param>
    /// <param name="messageType">The wire name.</param>
    /// <param name="occurredAt">When the producing change was committed.</param>
    /// <param name="headers">Trace and correlation headers; null means none.</param>
    public MessageContext(
        string messageId,
        string messageType,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        MessageId = messageId;
        MessageType = messageType;
        OccurredAt = occurredAt;
        Headers = headers ?? ReadOnlyDictionary<string, string>.Empty;
    }

    /// <summary>Stable id of the message. Never empty.</summary>
    public string MessageId { get; }

    /// <summary>The wire name. Never empty.</summary>
    public string MessageType { get; }

    /// <summary>When the producing change was committed.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Trace and correlation headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
}

/// <summary>A serialized message as it leaves the outbox and is handed to a transport.</summary>
public sealed record MessageEnvelope
{
    /// <summary>Creates the envelope.</summary>
    /// <param name="messageId">Stable id, unchanged across retries.</param>
    /// <param name="messageType">The wire name.</param>
    /// <param name="payload">The serialized integration event (JSON).</param>
    /// <param name="occurredAt">When the producing change was committed.</param>
    /// <param name="headers">Trace and correlation headers; null means none.</param>
    public MessageEnvelope(
        string messageId,
        string messageType,
        string payload,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(payload);
        MessageId = messageId;
        MessageType = messageType;
        Payload = payload;
        OccurredAt = occurredAt;
        Headers = headers ?? ReadOnlyDictionary<string, string>.Empty;
    }

    /// <summary>Stable id of the message. Never empty.</summary>
    public string MessageId { get; }

    /// <summary>The wire name. Never empty.</summary>
    public string MessageType { get; }

    /// <summary>The serialized integration event.</summary>
    public string Payload { get; }

    /// <summary>When the producing change was committed.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Trace and correlation headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>The metadata a consumer of this envelope sees.</summary>
    public MessageContext ToContext() => new(MessageId, MessageType, OccurredAt, Headers);
}
