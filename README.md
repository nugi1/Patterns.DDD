# Patterns.DDD

Small Domain-Driven Design building blocks for .NET 8 and .NET 10, in three packages you take
only as far as you need:

| Package | Depends on | What it gives you |
| --- | --- | --- |
| `Patterns.DDD` | nothing | Entities, aggregates, domain events, `Result`, rules |
| `Patterns.DDD.Application` | `Patterns.DDD` | Unit of work, integration events, domain-to-integration event map |
| `Patterns.DDD.MassTransit` | the above + MassTransit v8 EF Core | Domain events → MassTransit's transactional outbox; inbox on consumers |

All three are versioned together.

```bash
dotnet add package Patterns.DDD
```

## What's inside

| Type | Purpose |
| --- | --- |
| `Entity<TId>` | Identity-based equality |
| `AggregateRoot<TId>` | Consistency boundary that records domain events |
| `IDomainEvent` | Marker for things that happened |
| `Result`, `Result<T>`, `Error` | Expected failures without exceptions; `Map`, `Bind`, `Ensure`, `Match` |
| `IRule<TContext>`, `Rules` | Named business rules, composed with `All`, `When`, `ForEach`; evaluated in order, stopping at the first failure |
| `IHasDomainEvents` | Non-generic access to pending events for infrastructure (outbox) |
| `DomainException` | Broken invariants that indicate a bug |

There is deliberately no `ValueObject` base class (use `record`) and no generic repository
(define the exact persistence interface each aggregate needs).

## Example

```csharp
public sealed record OrderPaid(Guid OrderId) : IDomainEvent;

public sealed class Order(Guid id) : AggregateRoot<Guid>(id)
{
    public bool IsPaid { get; private set; }

    public Result Pay()
    {
        if (IsPaid)
            return new Error("order-already-paid", "Order is already paid");

        IsPaid = true;
        Raise(new OrderPaid(Id));
        return Result.Success();
    }
}

public sealed class AmountIsPositive : IRule<decimal>
{
    public Result Check(decimal amount) =>
        amount > 0 ? Result.Success() : new Error("amount-not-positive", "Amount must be > 0");
}
```

Compose rules and chain results. Evaluation order is part of the contract: the first broken
rule's `Error` is what you get back.

```csharp
IRule<Order> orderRules = Rules.All(
    new HasItems(),
    Rules.ForEach((Order o) => o.Lines, new QuantityIsPositive()),
    Rules.When((Order o) => o.IsExpress, new FitsExpressLimits()));

Result<Receipt> receipt = Parse(request)
    .Ensure(orderRules)
    .Bind(order => order.Pay().Map(() => new Receipt(order.Id)));

return receipt.Match(r => Results.Ok(r), e => Results.BadRequest(e.Code));
```

Without the outbox package, dequeue events after saving and dispatch them yourself:

```csharp
await repository.SaveAsync(order);
foreach (var e in order.DequeueDomainEvents())
    await dispatcher.Dispatch(e);
```

That loses events if the process dies between the save and the dispatch. The EF Core package closes
that gap.

## Application layer

Domain events are internal and change freely. Integration events are a published contract, and they
change by adding a new version. `IntegrationEventMap` is the one place where one becomes the other;
unmapped domain events never leave the bounded context. Give each contract a stable wire name with
MassTransit's `[MessageUrn]` so renaming the class never breaks a consumer.

```csharp
[MessageUrn("minibtc.block-connected.v1")]
public sealed record BlockConnectedV1(string BlockHash, int Height) : IIntegrationEvent;

map.Map((BlockConnected e) => new BlockConnectedV1(e.Hash.ToString(), e.Height));
```

`IUnitOfWork.SaveChangesAsync` has the same signature as `DbContext.SaveChangesAsync`, so a DbContext
implements it just by declaring it.

## Outbox and inbox with MassTransit (EF Core + PostgreSQL + RabbitMQ)

```csharp
// Program.cs
builder.Services.AddDbContext<NodeDbContext>((sp, o) => o.UseNpgsql(cs).UseDomainEventOutbox(sp));
builder.Services.AddMassTransit(x =>
{
    x.AddDomainEventOutbox<NodeDbContext>(map => map
        .Map((BlockConnected e) => new BlockConnectedV1(e.Hash.ToString(), e.Height)));

    x.AddConsumer<PushBlockToSignalRConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
        cfg.ConfigureEndpoints(context);
    });
});

// NodeDbContext.OnModelCreating, last line
modelBuilder.AddDomainEventOutbox();
```

**Publishing.** When `SaveChangesAsync` starts, an interceptor maps every tracked aggregate's pending
domain events and publishes them through the scoped `IPublishEndpoint`.

- With MassTransit's EF Core outbox, that endpoint adds outbox rows to the same DbContext. They commit
  or roll back together with the aggregate.
- MassTransit's delivery service then sends them to RabbitMQ.
- Events are cleared from aggregates only after the save succeeds. A failed save keeps them, and
  retrying the save on the same context does not publish them twice.
- `IBus` is never used, because it bypasses the outbox.
- Synchronous `SaveChanges` with pending events throws, because publishing to the outbox is async.

**Inbox.** Every endpoint configured by `ConfigureEndpoints` gets MassTransit's EF Core inbox and
outbox:

- A redelivered message with the same `MessageId` is skipped for that consumer endpoint.
- The consumer's DbContext changes, the inbox record, and any events raised while handling it are
  saved in one transaction after the consumer returns.

For notifications that come from outside, such as Bitcoin Core ZMQ, derive the id from a natural key:

```csharp
await publishEndpoint.Publish(new BlockNotified(hash),
    ctx => ctx.MessageId = MessageIds.FromKey($"block:{hash}"));
```

The inbox remembers ids for the outbox's `DuplicateDetectionWindow` (MassTransit's default is 30
minutes). Raise it if redeliveries can arrive later than that.

**Delivery and order.** Delivery is at-least-once, so consumers must be idempotent, which is what the
inbox gives you. Messages saved in one transaction are delivered in order. Across transactions, and
with concurrent consumers, order is not guaranteed. A consumer that needs order should use a field such
as block height, or run with `ConcurrentMessageLimit = 1`.

### EF Core versions

On .NET 10 everything runs on EF Core 10. On .NET 8, MassTransit 8.5 brings in EF Core 9, so
use EF Core 9 builds of your other EF packages there too, for example `Npgsql.EntityFrameworkCore.PostgreSQL`
9.x. An EF Core 8 provider fails at runtime with a `TypeLoadException`.

### Licensing

This package depends on **MassTransit v8**, which is Apache 2.0. MassTransit v9 and later are
commercially licensed, and the v8 line was announced to receive security patches through 2026.
Moving this package to v9 would require every consumer of it to hold a MassTransit license, so it will
only happen as a deliberate major version.

## Releasing

Push a tag `vX.Y.Z`; the publish workflow tests, packs every package at that version and pushes
them to nuget.org.
It needs a repository secret `NUGET_API_KEY`.

## License

MIT
