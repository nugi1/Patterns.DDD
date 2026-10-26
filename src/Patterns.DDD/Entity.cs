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

    /// <summary>True while the identity is still the type's default (e.g. <see cref="Guid.Empty"/>, 0).</summary>
    private bool IsTransient => EqualityComparer<TId>.Default.Equals(Id, default);

    /// <summary>
    /// Two entities are equal when they have the same concrete type and identity. Entities whose
    /// identity is still the default are only equal to themselves.
    /// </summary>
    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (other.GetType() != GetType() || IsTransient || other.IsTransient)
            return false;

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>Identity equality.</summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => left?.Equals(right) ?? right is null;

    /// <summary>Identity inequality.</summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
