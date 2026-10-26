namespace Patterns.DDD.Application;

/// <summary>
/// Commits everything a use case changed as one atomic unit: aggregate state and, when an outbox is
/// configured, the integration messages those changes produced.
/// </summary>
/// <remarks>
/// The signature matches <c>DbContext.SaveChangesAsync(CancellationToken)</c>, so an EF Core context
/// implements this interface just by declaring it.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes in one transaction; returns the number of rows written.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
