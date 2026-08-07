namespace BuzzMe.Domain.Occurrences;

/// <summary>An Occurrence's identity — same pattern as BoardId/ReminderId.</summary>
public readonly record struct OccurrenceId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
