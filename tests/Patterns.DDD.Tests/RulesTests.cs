namespace Patterns.DDD.Tests;

public class RulesTests
{
    private sealed record Order(bool IsExpress, int[] Quantities);

    private sealed class Positive : IRule<int>
    {
        public Result Check(int value) =>
            value > 0 ? Result.Success() : new Error("not-positive", $"{value} must be > 0");
    }

    private sealed class HasItems : IRule<Order>
    {
        public Result Check(Order order) =>
            order.Quantities.Length > 0 ? Result.Success() : new Error("empty", "No items");
    }

    private sealed class SmallEnoughForExpress : IRule<Order>
    {
        public Result Check(Order order) =>
            order.Quantities.Length <= 2 ? Result.Success() : new Error("too-many-for-express", "Max 2 items");
    }

    private static readonly IRule<Order> OrderRules = Rules.All(
        new HasItems(),
        Rules.ForEach((Order o) => o.Quantities, new Positive()),
        Rules.When((Order o) => o.IsExpress, new SmallEnoughForExpress()));

    [Fact]
    public void Composite_passes_when_every_rule_passes() =>
        Assert.True(OrderRules.Check(new Order(IsExpress: true, [1, 2])).IsSuccess);

    [Fact]
    public void Composite_reports_the_first_broken_rule_in_order() =>
        // ForEach would also pass here; HasItems is first, so its error is the one reported.
        Assert.Equal("empty", OrderRules.Check(new Order(IsExpress: true, [])).Error?.Code);

    [Fact]
    public void ForEach_stops_at_first_failing_item() =>
        Assert.Equal("-1 must be > 0", OrderRules.Check(new Order(false, [3, -1, 0])).Error?.Message);

    [Fact]
    public void When_skips_rule_if_condition_is_false() =>
        Assert.True(OrderRules.Check(new Order(IsExpress: false, [1, 2, 3])).IsSuccess);

    [Fact]
    public void All_rejects_null_rules() =>
        Assert.Throws<ArgumentException>(() => Rules.All<int>(new Positive(), null!));
}
