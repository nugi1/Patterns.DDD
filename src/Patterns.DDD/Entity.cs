namespace Patterns.DDD;

/// <summary>An object defined by its identity, not by its attributes.</summary>
/// <typeparam name="TId">The identity type.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>Creates the entity with its identity.</summary>
    protected Entity(TId id) => Id = id;

    /// <summary>The identity that makes this entity unique.</summary>
    public TId Id { get; }

    /// <summary>Two entities are equal when they have the same concrete type and identity.</summary>
    public bool Equals(Entity<TId>? other) =>
        other is not null
        && other.GetType() == GetType()
        && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
