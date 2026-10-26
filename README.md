# Patterns.DDD

Small Domain-Driven Design building blocks for .NET 8 and .NET 10, in three packages you take
only as far as you need:

| Package | Depends on | What it gives you |
| --- | --- | --- |
| `Patterns.DDD` | nothing | Entities, aggregates, domain events, `Result`, rules |
| `Patterns.DDD.Application` | `Patterns.DDD` | Unit of work, integration events, message envelopes, publishers, handlers, event mapping |
| `Patterns.DDD.EntityFrameworkCore` | the above + EF Core | Transactional outbox and inbox |

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

Dequeue events after saving, then publish them:

```csharp
await repository.SaveAsync(order);
foreach (var e in order.DequeueDomainEvents())
    await publisher.Publish(e);
```

## Application layer

Domain events are internal and change freely. Integration events are a published contract with a
stable wire name, and they change by adding a new version. `IntegrationEventMap` is the one place
where one becomes the other; unmapped domain events never leave the bounded context.

```csharp
[MessageType("minibtc.block-connected.v1")]
public sealed record BlockConnectedV1(string BlockHash, int Height) : IIntegrationEvent;

var map = new IntegrationEventMap()
    .Map((BlockConnected e) => new BlockConnectedV1(e.Hash.ToString(), e.Height));
```

Delivery is at-least-once. `IMessagePublisher` returns only after the transport accepted a message,
and `IMessageHandler<T>` must be idempotent or run behind the inbox. `MessageContext.MessageId` is
the deduplication key.

## Releasing

Push a tag `vX.Y.Z`; the publish workflow tests, packs every package at that version and pushes
them to nuget.org.
It needs a repository secret `NUGET_API_KEY`.

## License

MIT
