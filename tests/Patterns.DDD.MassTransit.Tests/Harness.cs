using System.Collections.Concurrent;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Patterns.DDD.Application;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Patterns.DDD.MassTransit.Tests;

// ---- A tiny domain: a ledger aggregate whose deposits are published. ----

public sealed record Deposited(Guid LedgerId, long Amount, long Balance) : IDomainEvent;

[MessageUrn("test.deposited.v1")]
public sealed record DepositedV1(Guid LedgerId, long Amount, long Balance) : IIntegrationEvent;

/// <summary>An external notification, like a ZMQ hashblock, deduplicated by a natural key.</summary>
[MessageUrn("test.block-notified.v1")]
public sealed record BlockNotified(string Hash, Guid LedgerId);

public sealed class Ledger : AggregateRoot<Guid>
{
    public Ledger(Guid id) : base(id) { }

    public long Balance { get; private set; }

    public void Deposit(long amount)
    {
        Balance += amount;
        Raise(new Deposited(Id, amount, Balance));
    }
}

// Implements IUnitOfWork with no code: the signatures match.
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Ledger> Ledgers => Set<Ledger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ledger>().HasKey(l => l.Id);
        modelBuilder.AddDomainEventOutbox();
    }
}

// ---- Consumers record what reached them through RabbitMQ. ----

public sealed class Received
{
    public ConcurrentQueue<DepositedV1> Deposits { get; } = new();
    public ConcurrentQueue<BlockNotified> Blocks { get; } = new();
}

public sealed class DepositedConsumer(Received received) : IConsumer<DepositedV1>
{
    public Task Consume(ConsumeContext<DepositedV1> context)
    {
        received.Deposits.Enqueue(context.Message);
        return Task.CompletedTask;
    }
}

/// <summary>Like a ZMQ block handler: changes an aggregate, whose events go out via the consumer's outbox.</summary>
public sealed class BlockConsumer(Received received, LedgerDbContext db) : IConsumer<BlockNotified>
{
    public async Task Consume(ConsumeContext<BlockNotified> context)
    {
        received.Blocks.Enqueue(context.Message);
        var ledger = await db.Ledgers.FindAsync(context.Message.LedgerId) ?? db.Ledgers.Add(new Ledger(context.Message.LedgerId)).Entity;
        ledger.Deposit(1);
        // No SaveChanges here: the endpoint's EF outbox saves after the consumer, with the inbox row.
    }
}

// ---- Shared containers; each test gets its own host, queues and clean tables. ----

public sealed class Infrastructure : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder().WithImage("rabbitmq:4-alpine").Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());
        await using var host = await StartHostAsync("init");
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    /// <summary>A running app: MassTransit on RabbitMQ, EF outbox on PostgreSQL. Queues are prefixed per test.</summary>
    public async Task<TestHost> StartHostAsync(string queuePrefix)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<Received>();

        builder.Services.AddDbContext<LedgerDbContext>((sp, options) => options
            .UseNpgsql(_postgres.GetConnectionString())
            .UseDomainEventOutbox(sp)
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

        builder.Services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(queuePrefix, includeNamespace: false));

            x.AddDomainEventOutbox<LedgerDbContext>(
                map => map.Map((Deposited e) => new DepositedV1(e.LedgerId, e.Amount, e.Balance)),
                outbox => outbox.QueryDelay = TimeSpan.FromMilliseconds(100));

            x.AddConsumer<DepositedConsumer>();
            x.AddConsumer<BlockConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(_rabbit.GetConnectionString()));
                cfg.ConfigureEndpoints(context);
            });
        });

        var host = builder.Build();
        await host.StartAsync();
        return new TestHost(host);
    }

    public async Task ResetDatabaseAsync()
    {
        await using var host = await StartHostAsync("reset");
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.ExecuteSqlRawAsync(
            """
            DO $$ DECLARE t record; BEGIN
              FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
                EXECUTE 'TRUNCATE TABLE ' || quote_ident(t.tablename) || ' CASCADE';
              END LOOP;
            END $$;
            """);
    }
}

public sealed class TestHost(IHost host) : IAsyncDisposable
{
    public IServiceProvider Services => host.Services;

    public Received Received => host.Services.GetRequiredService<Received>();

    public async Task InScopeAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }

    /// <summary>Waits until <paramref name="done"/> holds, failing after 20 seconds.</summary>
    public static async Task EventuallyAsync(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!done())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Timed out waiting for messages.");
            await Task.Delay(100);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class InfrastructureCollection : ICollectionFixture<Infrastructure>
{
    public const string Name = "infrastructure";
}
