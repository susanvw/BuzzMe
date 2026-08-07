namespace BuzzMe.Domain.Boards;

/// <summary>A Board's identity — IMPLEMENTATION_SPEC.md §1 asks for this to be its own type, not a bare Guid, so a BoardId can never be confused with any other aggregate's id at compile time.</summary>
public readonly record struct BoardId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
