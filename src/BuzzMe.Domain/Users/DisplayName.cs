namespace BuzzMe.Domain.Users;

/// <summary>Same reasoning as Board's BoardName — "non-empty" is format validation belonging at the Api boundary; this constructor is a defensive guard against an invariant actually being violated.</summary>
public sealed record DisplayName
{
    public string Value { get; }

    public DisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A User's display name cannot be empty.", nameof(value));

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
