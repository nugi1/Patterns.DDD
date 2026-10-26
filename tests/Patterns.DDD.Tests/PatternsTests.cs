namespace Patterns.DDD.Tests;

public class PatternsTests
{
    private sealed record Ping : IDomainEvent;

    private sealed class Counter(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Tick() => Raise(new Ping());
    }

    private sealed class MustBePositive : IRule<int>
    {
        public Result Check(int value) =>
            value > 0 ? Result.Success() : new Error("not-positive", "Value must be > 0");
    }

    private sealed class MustBeEven : IRule<int>
    {
        public Result Check(int value) =>
            value % 2 == 0 ? Result.Success() : new Error("not-even", "Value must be even");
    }

    [Fact]
    public void Entities_with_same_id_are_equal()
    {
        var id = Guid.NewGuid();
        Assert.Equal(new Counter(id), new Counter(id));
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal() =>
        Assert.NotEqual(new Counter(Guid.NewGuid()), new Counter(Guid.NewGuid()));

    [Fact]
    public void Entities_with_default_id_are_only_equal_to_themselves()
    {
        var a = new Counter(Guid.Empty);
        var sameReference = a; // a == a directly is compiler error CS1718 under warnings-as-errors
        Assert.NotEqual(a, new Counter(Guid.Empty));
        Assert.True(a == sameReference);
        Assert.True(a != new Counter(Guid.Empty));
    }

    [Fact]
    public void Infrastructure_can_clear_events_through_the_interface()
    {
        var counter = new Counter(Guid.NewGuid());
        counter.Tick();

        IHasDomainEvents tracked = counter;
        Assert.Single(tracked.DomainEvents);
        tracked.ClearDomainEvents();
        Assert.Empty(counter.DomainEvents);
    }

    [Fact]
    public void Aggregate_records_and_dequeues_events()
    {
        var counter = new Counter(Guid.NewGuid());
        counter.Tick();
        counter.Tick();

        Assert.Equal(2, counter.DequeueDomainEvents().Count);
        Assert.Empty(counter.DomainEvents);
    }

    [Fact]
    public void Rules_stop_at_first_failure()
    {
        IRule<int>[] rules = [new MustBePositive(), new MustBeEven()];

        var result = Rules.CheckAll(-3, rules);

        Assert.True(result.IsFailure);
        Assert.Equal("not-positive", result.Error?.Code);
    }

    [Fact]
    public void Rules_pass_when_all_pass()
    {
        IRule<int>[] rules = [new MustBePositive(), new MustBeEven()];
        Assert.True(Rules.CheckAll(4, rules).IsSuccess);
    }

    [Fact]
    public void Value_converts_to_successful_result()
    {
        Result<int> result = 42;
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failed_result_has_no_value()
    {
        Result<int> result = new Error("x", "boom");
        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
