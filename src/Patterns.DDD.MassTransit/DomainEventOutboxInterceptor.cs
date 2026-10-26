using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Patterns.DDD.Application;

namespace Patterns.DDD.MassTransit;

/// <summary>
/// When a <c>SaveChangesAsync</c> starts, maps every tracked aggregate's pending domain events to
/// integration events and publishes them through the scoped <see cref="IPublishEndpoint"/>. With
/// MassTransit's EF Core outbox that endpoint writes outbox rows into this same DbContext, so they
/// commit or roll back together with the aggregate's changes.
/// </summary>
/// <remarks>
/// <para>
/// Registered per scope. The publish endpoint is resolved when a save actually has events, not in the
/// constructor: MassTransit's outbox endpoint itself depends on the DbContext, so resolving it eagerly
/// would be a dependency cycle.
/// </para>
/// <para>
/// Inside a consumer whose endpoint uses the EF outbox, the scoped endpoint is the consumer's outbox,
/// so events raised while handling a message are delivered only if that handling commits. Elsewhere
/// it is the bus outbox (<c>UseBusOutbox</c>). It is never <see cref="IBus"/>, which bypasses the outbox.
/// </para>
/// <para>
/// Events are cleared from aggregates only after the save succeeds. Events already handed to the outbox
/// by a failed save stay tracked as outbox rows and are not published again if the same context retries.
/// </para>
/// </remarks>
public sealed class DomainEventOutboxInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _services;
    private readonly IntegrationEventMap _map;
    private readonly HashSet<IDomainEvent> _published = new(ReferenceEqualityComparer.Instance);
    private List<IHasDomainEvents>? _saving;

    /// <summary>Creates the interceptor for one scope.</summary>
    public DomainEventOutboxInterceptor(IServiceProvider services, IntegrationEventMap map)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(map);
        _services = services;
        _map = map;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not { } context)
            return result;

        var aggregates = AggregatesWithEvents(context);
        if (aggregates.Count == 0)
            return result;

        var publisher = _services.GetRequiredService<IPublishEndpoint>();
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                if (!_published.Add(domainEvent))
                    continue; // already in the outbox from an earlier, failed attempt of this save

                foreach (var integrationEvent in _map.Translate(domainEvent))
                {
                    // Runtime type, so consumers of the concrete contract receive it.
                    await publisher.Publish(integrationEvent, integrationEvent.GetType(), cancellationToken);
                }
            }
        }

        _saving = aggregates;
        return result;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context
            && AggregatesWithEvents(context).Any(a => a.DomainEvents.Any(e => !_published.Contains(e))))
        {
            throw new InvalidOperationException(
                "Aggregates have unpublished domain events. Use SaveChangesAsync: publishing to the " +
                "MassTransit outbox is asynchronous.");
        }

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ClearSaved();
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ClearSaved();
        return result;
    }

    private void ClearSaved()
    {
        if (_saving is null)
            return;

        foreach (var aggregate in _saving)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
                _published.Remove(domainEvent);
            aggregate.ClearDomainEvents();
        }

        _saving = null;
    }

    private static List<IHasDomainEvents> AggregatesWithEvents(DbContext context) =>
        context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();
}
