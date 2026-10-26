namespace Patterns.DDD.Application;

/// <summary>
/// Declares which domain events leave the bounded context, and as which integration events.
/// </summary>
/// <remarks>
/// Domain events are internal and may change freely; integration events are published contracts.
/// This map is the one place where one becomes the other. A domain event with no mapping is never
/// published. Configure it once at startup; it is read-only afterwards and safe to share.
/// </remarks>
public sealed class IntegrationEventMap
{
    private readonly Dictionary<Type, List<Func<IDomainEvent, IIntegrationEvent?>>> _maps = [];
    private bool _frozen;

    /// <summary>
    /// Publishes <typeparamref name="TDomainEvent"/> as <typeparamref name="TIntegrationEvent"/>.
    /// Return null from <paramref name="map"/> to skip a particular event. Registering several maps for
    /// one domain event publishes several integration events, in registration order.
    /// </summary>
    /// <exception cref="InvalidOperationException">The map is already in use.</exception>
    public IntegrationEventMap Map<TDomainEvent, TIntegrationEvent>(Func<TDomainEvent, TIntegrationEvent?> map)
        where TDomainEvent : IDomainEvent
        where TIntegrationEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(map);
        if (_frozen)
            throw new InvalidOperationException("The map is in use; configure it before the first Translate.");

        if (!_maps.TryGetValue(typeof(TDomainEvent), out var maps))
            _maps[typeof(TDomainEvent)] = maps = [];

        maps.Add(e => map((TDomainEvent)e));
        return this;
    }

    /// <summary>The integration events to publish for <paramref name="domainEvent"/>; empty when none.</summary>
    /// <remarks>Matches the event's exact runtime type, so mappings never fire by accident through inheritance.</remarks>
    public IReadOnlyList<IIntegrationEvent> Translate(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _frozen = true;

        if (!_maps.TryGetValue(domainEvent.GetType(), out var maps))
            return [];

        var events = new List<IIntegrationEvent>(maps.Count);
        foreach (var map in maps)
        {
            if (map(domainEvent) is { } integrationEvent)
                events.Add(integrationEvent);
        }

        return events;
    }
}
