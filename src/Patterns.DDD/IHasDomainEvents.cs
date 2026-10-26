namespace Patterns.DDD;

/// <summary>
/// Non-generic view of an aggregate's pending events, so infrastructure (e.g. an outbox) can collect
/// them from every tracked aggregate without knowing each aggregate's identity type.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Events raised and not yet cleared.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Forgets the pending events. Infrastructure calls this once they are durably stored.</summary>
    void ClearDomainEvents();
}
