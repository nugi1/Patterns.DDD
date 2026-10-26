namespace Patterns.DDD.Application;

/// <summary>
/// Commits everything a use case changed as one atomic unit: aggregate state and, when an outbox is
/// configured, the integration messages those changes produced.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes in one transaction.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
