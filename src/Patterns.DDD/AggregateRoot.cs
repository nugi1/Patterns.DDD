namespace Patterns.DDD;

/// <summary>
/// The consistency boundary: all changes go through the root, which guards the invariants
/// and records what happened as domain events.
/// </summary>
/// <typeparam name="TId">The identity type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Creates the aggregate root with its identity.</summary>
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Events raised since they were last dequeued.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Records that something happened inside the aggregate.</summary>
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Returns the recorded events and clears them. Call after the aggregate's changes are
    /// persisted, so nothing is published for a change that failed to save.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }
}
