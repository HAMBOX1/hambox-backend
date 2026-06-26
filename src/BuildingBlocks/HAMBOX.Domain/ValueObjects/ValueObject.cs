namespace HAMBOX.Domain.ValueObjects;

/// <summary>
/// Represents a value object whose equality is based on its component values.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Determines whether two value objects are equal.
    /// </summary>
    /// <param name="left">The first value object.</param>
    /// <param name="right">The second value object.</param>
    /// <returns><see langword="true"/> when both value objects are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two value objects are not equal.
    /// </summary>
    /// <param name="left">The first value object.</param>
    /// <param name="right">The second value object.</param>
    /// <returns><see langword="true"/> when both value objects are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(ValueObject? other)
    {
        return other is not null
            && other.GetType() == GetType()
            && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValueObject valueObject && Equals(valueObject);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hashCode = default;

        foreach (object? component in GetEqualityComponents())
        {
            hashCode.Add(component);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Gets the components that define equality for the value object.
    /// </summary>
    /// <returns>The sequence of equality components.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();
}
