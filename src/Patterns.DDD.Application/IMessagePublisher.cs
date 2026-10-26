namespace Patterns.DDD.Application;

/// <summary>
/// Hands a message to a transport (a broker, SignalR, an HTTP webhook). Implemented once per transport;
/// the outbox dispatcher is its only caller.
/// </summary>
/// <remarks>
/// Return only once the transport has accepted the message. The dispatcher marks the outbox row as
/// processed as soon as this returns, so returning early can lose a message. Throw on failure: the
/// row is retried. Retries mean the same <see cref="MessageEnvelope.MessageId"/> may be sent more
/// than once (at-least-once delivery); consumers deduplicate on it.
/// </remarks>
public interface IMessagePublisher
{
    /// <summary>Publishes one message; throws if the transport did not accept it.</summary>
    Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);
}
