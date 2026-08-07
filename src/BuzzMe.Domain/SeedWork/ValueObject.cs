namespace BuzzMe.Domain.SeedWork;

/// <summary>
/// Base type for anything with value equality and no independent identity
/// (e.g. a future RecurrenceRule or ReferenceTimezone). In practice most Value Objects
/// should simply be C# `record` types, which already have structural equality for free —
/// this base exists only for the rare case a Value Object needs to be a `class`
/// (mutable-looking wrapper, inheritance) while still behaving like one.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, (hash, component) => HashCode.Combine(hash, component));

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
