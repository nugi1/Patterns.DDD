using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Patterns.DDD.Application;

namespace Patterns.DDD.MassTransit;

/// <summary>Wires Patterns.DDD aggregates into MassTransit's EF Core outbox and inbox.</summary>
public static class Registration
{
    /// <summary>
    /// Inside <c>AddMassTransit</c>: registers the domain-to-integration event map and the save
    /// interceptor, MassTransit's EF Core outbox on PostgreSQL with the bus outbox, and the inbox/outbox
    /// on every consumer endpoint configured by <c>ConfigureEndpoints</c>.
    /// </summary>
    /// <param name="configurator">The MassTransit registration.</param>
    /// <param name="configureMap">Which domain events are published, as which integration events.</param>
    /// <param name="configureOutbox">Optional outbox settings, e.g. <c>QueryDelay</c> or <c>DuplicateDetectionWindow</c>.</param>
    public static IBusRegistrationConfigurator AddDomainEventOutbox<TDbContext>(
        this IBusRegistrationConfigurator configurator,
        Action<IntegrationEventMap> configureMap,
        Action<IEntityFrameworkOutboxConfigurator>? configureOutbox = null)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(configureMap);

        var map = new IntegrationEventMap();
        configureMap(map);
        configurator.TryAddSingleton(map);
        configurator.TryAddScoped<DomainEventOutboxInterceptor>();

        configurator.AddEntityFrameworkOutbox<TDbContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.UseBusOutbox();
            configureOutbox?.Invoke(outbox);
        });

        configurator.AddConfigureEndpointsCallback((context, _, endpoint) =>
            endpoint.UseEntityFrameworkOutbox<TDbContext>(context));

        return configurator;
    }

    /// <summary>Attaches the interceptor; use inside <c>AddDbContext((sp, options) => ...)</c>.</summary>
    public static DbContextOptionsBuilder UseDomainEventOutbox(this DbContextOptionsBuilder builder, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(services);
        return builder.AddInterceptors(services.GetRequiredService<DomainEventOutboxInterceptor>());
    }

    /// <summary>
    /// Adds MassTransit's inbox and outbox tables and stops EF Core from mapping aggregates' pending
    /// <see cref="IHasDomainEvents.DomainEvents"/>. Call it last in <c>OnModelCreating</c>.
    /// </summary>
    public static ModelBuilder AddDomainEventOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.BaseType is null
                && !entityType.IsOwned()
                && typeof(IHasDomainEvents).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Ignore(nameof(IHasDomainEvents.DomainEvents));
            }
        }

        return modelBuilder;
    }
}
