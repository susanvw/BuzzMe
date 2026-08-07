namespace BuzzMe.Domain.Boards;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — "name non-empty" is a Board invariant. Whether a name is
/// present is *format* validation and belongs at the Api boundary (FluentValidation,
/// DEVELOPMENT_GUIDE.md §9) — by the time a name reaches this constructor it should
/// already be non-empty, so this check is a defensive guard against an invariant actually
/// being violated, not a duplicate of the API's validation. That's exactly the case
/// DEVELOPMENT_GUIDE.md §9 reserves exceptions for.
/// </summary>
public sealed record BoardName
{
    public string Value { get; }

    public BoardName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A Board's name cannot be empty.", nameof(value));

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
