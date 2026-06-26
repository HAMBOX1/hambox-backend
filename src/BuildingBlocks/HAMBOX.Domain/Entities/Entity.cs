namespace HAMBOX.Domain.Entities;

/// <summary>
/// Represents a domain entity with identity.
/// </summary>
public abstract class Entity : BaseEntity, IEquatable<Entity>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Entity"/> class.
    /// </summary>
    protected Entity()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity"/> class.
    /// </summary>
    /// <param name="id">The unique entity identifier.</param>
    protected Entity(Guid id)
        : base(id)
    {
    }

    /// <summary>
    /// Determines whether two entities are equal.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns><see langword="true"/> when both entities are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two entities are not equal.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns><see langword="true"/> when both entities are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);

    /// <inheritdoc />
    /// <remarks>
    /// Transient entities (those with <see cref="BaseEntity.Id"/> equal to <see cref="Guid.Empty"/>)
    /// are never considered equal by value. Only the <see cref="object.ReferenceEquals"/> check can
    /// return <see langword="true"/> for transient instances.
    /// </remarks>
    public bool Equals(Entity? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        return Id != Guid.Empty && Id == other.Id;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity entity && Equals(entity);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
