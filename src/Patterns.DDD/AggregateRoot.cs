namespace Patterns.DDD;

/// <summary>
/// The consistency boundary: all changes go through the root, which guards the invariants
/// and records what happened as domain events.
/// </summary>
/// <typeparam name="TId">The identity type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Creates the aggregate root with its identity.</summary>
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Events raised since they were last dequeued or cleared.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Records that something happened inside the aggregate.</summary>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Returns the recorded events and clears them. Use this when you dispatch events yourself;
    /// with an outbox, infrastructure reads <see cref="DomainEvents"/> and clears them once stored.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }

    // Explicit: domain code never sees a "clear" button on the aggregate; only infrastructure,
    // holding the interface, can drop events.
    void IHasDomainEvents.ClearDomainEvents() => _domainEvents.Clear();
}
