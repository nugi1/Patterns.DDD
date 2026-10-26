using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Patterns.DDD.MassTransit.Tests;

[Collection(InfrastructureCollection.Name)]
public sealed class OutboxTests(Infrastructure infrastructure) : IAsyncLifetime
{
    public Task InitializeAsync() => infrastructure.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Saving_an_aggregate_publishes_its_events_through_the_outbox()
    {
        await using var app = await infrastructure.StartHostAsync(nameof(Saving_an_aggregate_publishes_its_events_through_the_outbox));
        var ledger = new Ledger(Guid.NewGuid());
        ledger.Deposit(5);
        ledger.Deposit(7);

        await app.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<LedgerDbContext>();
            db.Ledgers.Add(ledger);
            await db.SaveChangesAsync();
        });

        Assert.Empty(ledger.DomainEvents);
        await TestHost.EventuallyAsync(() => app.Received.Deposits.Count == 2);
        Assert.Equal(new[] { 5L, 12L }, app.Received.Deposits.Select(d => d.Balance).Order());
    }

    [Fact]
    public async Task A_failed_save_publishes_nothing_and_keeps_the_events()
    {
        await using var app = await infrastructure.StartHostAsync(nameof(A_failed_save_publishes_nothing_and_keeps_the_events));
        var id = Guid.NewGuid();
        await app.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<LedgerDbContext>();
            db.Ledgers.Add(new Ledger(id));
            await db.SaveChangesAsync();
        });

        var duplicate = new Ledger(id); // same key: the insert fails
        duplicate.Deposit(3);

        await app.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<LedgerDbContext>();
            db.Ledgers.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });

        Assert.Single(duplicate.DomainEvents);
        await Task.Delay(TimeSpan.FromSeconds(2)); // give a wrongly committed message time to arrive
        Assert.Empty(app.Received.Deposits);
    }

    [Fact]
    public async Task Retrying_a_failed_save_on_the_same_context_publishes_each_event_once()
    {
        await using var app = await infrastructure.StartHostAsync(nameof(Retrying_a_failed_save_on_the_same_context_publishes_each_event_once));
        var taken = Guid.NewGuid();
        await app.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<LedgerDbContext>();
            db.Ledgers.Add(new Ledger(taken));
            await db.SaveChangesAsync();
        });

        var ledger = new Ledger(Guid.NewGuid());
        ledger.Deposit(1);

        await app.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<LedgerDbContext>();
            db.Ledgers.Add(ledger);
            db.Ledgers.Add(new Ledger(taken)); // makes the first save fail
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

            db.ChangeTracker.Entries<Ledger>().Single(e => e.Entity.Id == taken).State = EntityState.Detached;
            await db.SaveChangesAsync();
        });

        await TestHost.EventuallyAsync(() => app.Received.Deposits.Count >= 1);
        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Single(app.Received.Deposits);
    }

    [Fact]
    public async Task Synchronous_SaveChanges_with_pending_events_is_rejected()
    {
        await using var app = await infrastructure.StartHostAsync(nameof(Synchronous_SaveChanges_with_pending_events_is_rejected));
        var ledger = new Ledger(Guid.NewGuid());
        ledger.Deposit(1);

        await app.InScopeAsync(sp =>
        {
            var db = sp.GetRequiredService<LedgerDbContext>();
            db.Ledgers.Add(ledger);
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            return Task.CompletedTask;
        });
    }
}
