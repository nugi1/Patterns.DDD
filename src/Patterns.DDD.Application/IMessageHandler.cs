namespace Patterns.DDD.Application;

/// <summary>Handles one kind of incoming message.</summary>
/// <typeparam name="TMessage">An integration event, or any transport message (e.g. a ZMQ notification).</typeparam>
/// <remarks>
/// Delivery is at-least-once, so the same message can arrive twice. Either make the handler naturally
/// idempotent or run it behind an inbox, which records <see cref="MessageContext.MessageId"/> in the
/// same transaction as the handler's changes and skips messages it has already seen.
/// </remarks>
public interface IMessageHandler<in TMessage>
    where TMessage : notnull
{
    /// <summary>Applies the message's effects.</summary>
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken = default);
}
