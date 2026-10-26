namespace Patterns.DDD;

/// <summary>One named business rule checked against a context.</summary>
/// <typeparam name="TContext">What the rule inspects.</typeparam>
public interface IRule<in TContext>
{
    /// <summary>Returns success, or the error describing how the rule was broken.</summary>
    Result Check(TContext context);
}

/// <summary>Evaluating and composing rules. Every composite is itself an <see cref="IRule{TContext}"/>.</summary>
/// <remarks>
/// Evaluation is always ordered and fail-fast: the first broken rule's error is the result.
/// Order is part of the contract — callers that mirror an external system (e.g. a reference
/// implementation that reports its first reject reason) get the same error it would.
/// </remarks>
public static class Rules
{
    /// <summary>Runs rules in order and stops at the first failure.</summary>
    public static Result CheckAll<TContext>(TContext context, IEnumerable<IRule<TContext>> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var rule in rules)
        {
            var result = rule.Check(context);
            if (result.IsFailure)
                return result;
        }

        return Result.Success();
    }

    /// <summary>A rule that passes when all <paramref name="rules"/> pass, checked in order.</summary>
    public static IRule<TContext> All<TContext>(params IRule<TContext>[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (Array.IndexOf(rules, null) >= 0)
            throw new ArgumentException("Rules must not contain null.", nameof(rules));

        return new AllRule<TContext>([.. rules]); // copy: later changes to the caller's array don't leak in
    }

    /// <summary>A rule that only applies when <paramref name="condition"/> holds; otherwise it passes.</summary>
    public static IRule<TContext> When<TContext>(Func<TContext, bool> condition, IRule<TContext> rule)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(rule);
        return new WhenRule<TContext>(condition, rule);
    }

    /// <summary>Applies <paramref name="rule"/> to each item of the context, in order, stopping at the first failure.</summary>
    public static IRule<TContext> ForEach<TContext, TItem>(Func<TContext, IEnumerable<TItem>> items, IRule<TItem> rule)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(rule);
        return new ForEachRule<TContext, TItem>(items, rule);
    }

    private sealed class AllRule<TContext>(IRule<TContext>[] rules) : IRule<TContext>
    {
        public Result Check(TContext context) => CheckAll(context, rules);
    }

    private sealed class WhenRule<TContext>(Func<TContext, bool> condition, IRule<TContext> rule) : IRule<TContext>
    {
        public Result Check(TContext context) => condition(context) ? rule.Check(context) : Result.Success();
    }

    private sealed class ForEachRule<TContext, TItem>(Func<TContext, IEnumerable<TItem>> items, IRule<TItem> rule)
        : IRule<TContext>
    {
        public Result Check(TContext context)
        {
            foreach (var item in items(context))
            {
                var result = rule.Check(item);
                if (result.IsFailure)
                    return result;
            }

            return Result.Success();
        }
    }
}
