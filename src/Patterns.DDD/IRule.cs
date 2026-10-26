namespace Patterns.DDD;

/// <summary>One business rule checked against a context.</summary>
/// <typeparam name="TContext">What the rule inspects.</typeparam>
public interface IRule<in TContext>
{
    /// <summary>Returns success, or the error describing how the rule was broken.</summary>
    Result Check(TContext context);
}

/// <summary>Helpers for evaluating sets of rules.</summary>
public static class Rules
{
    /// <summary>Runs rules in order and stops at the first failure.</summary>
    public static Result CheckAll<TContext>(TContext context, IEnumerable<IRule<TContext>> rules)
    {
        foreach (var rule in rules)
        {
            var result = rule.Check(context);
            if (result.IsFailure)
                return result;
        }

        return Result.Success();
    }
}
