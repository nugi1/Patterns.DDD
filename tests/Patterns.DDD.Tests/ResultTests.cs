namespace Patterns.DDD.Tests;

public class ResultTests
{
    private static readonly Error Boom = new("boom", "it broke");

    private static Result<int> Half(int x) => x % 2 == 0 ? x / 2 : new Error("odd", $"{x} is odd");

    private sealed class LessThan(int max) : IRule<int>
    {
        public Result Check(int value) =>
            value < max ? Result.Success() : new Error("too-big", $"{value} >= {max}");
    }

    [Fact]
    public void Failure_rejects_null_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
    }

    [Fact]
    public void Error_requires_a_code() =>
        Assert.Throws<ArgumentException>(() => new Error(" ", "no code"));

    [Fact]
    public void Map_transforms_value_on_success() =>
        Assert.Equal(4, Result<int>.Success(2).Map(x => x * 2).Value);

    [Fact]
    public void Map_does_not_run_on_failure()
    {
        var called = false;
        Result<int> failed = Boom;

        var mapped = failed.Map(x => { called = true; return x.ToString(); });

        Assert.False(called);
        Assert.Same(Boom, mapped.Error);
    }

    [Fact]
    public void Bind_chains_and_stops_at_first_failure()
    {
        Assert.Equal(2, Result<int>.Success(8).Bind(x => Half(x)).Bind(x => Half(x)).Value);

        var failed = Result<int>.Success(6).Bind(x => Half(x)).Bind(x => Half(x));
        Assert.Equal("odd", failed.Error?.Code);
        Assert.Equal("3 is odd", failed.Error?.Message);
    }

    [Fact]
    public void Non_generic_bind_and_map()
    {
        var ok = Result.Success().Bind(() => Result.Success()).Map(() => 7);
        Assert.Equal(7, ok.Value);

        var failed = Result.Failure(Boom).Map(() => 7);
        Assert.Same(Boom, failed.Error);
    }

    [Fact]
    public void Match_handles_both_outcomes()
    {
        Assert.Equal("ok 3", Result<int>.Success(3).Match(v => $"ok {v}", e => e.Code));
        Assert.Equal("boom", Result<int>.Failure(Boom).Match(v => $"ok {v}", e => e.Code));
        Assert.Equal("fine", Result.Success().Match(() => "fine", e => e.Code));
    }

    [Fact]
    public void Ensure_applies_a_rule_to_the_value()
    {
        Assert.True(Result<int>.Success(3).Ensure(new LessThan(10)).IsSuccess);
        Assert.Equal("too-big", Result<int>.Success(30).Ensure(new LessThan(10)).Error?.Code);
        Assert.Same(Boom, Result<int>.Failure(Boom).Ensure(new LessThan(10)).Error);
    }
}
