# Patterns.DDD

Small, dependency-free Domain-Driven Design building blocks for .NET 8 and .NET 10.

```bash
dotnet add package Patterns.DDD
```

## What's inside

| Type | Purpose |
| --- | --- |
| `Entity<TId>` | Identity-based equality |
| `AggregateRoot<TId>` | Consistency boundary that records domain events |
| `IDomainEvent` | Marker for things that happened |
| `Result`, `Result<T>`, `Error` | Expected failures without exceptions |
| `IRule<TContext>`, `Rules.CheckAll` | Named business rules, evaluated in order, stopping at the first failure |
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

Dequeue events after saving, then publish them:

```csharp
await repository.SaveAsync(order);
foreach (var e in order.DequeueDomainEvents())
    await publisher.Publish(e);
```

## Releasing

Push a tag `vX.Y.Z`; the publish workflow tests, packs and pushes that version to nuget.org.
It needs a repository secret `NUGET_API_KEY`.

## License

MIT
