namespace BuzzMe.Domain.Reminders;

/// <summary>A Reminder's identity — same pattern as BoardId (IMPLEMENTATION_SPEC.md §1).</summary>
public readonly record struct ReminderId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
