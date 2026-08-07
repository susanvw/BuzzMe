namespace BuzzMe.Domain.Buzzes;

/// <summary>A Buzz's identity — same pattern as BoardId/ReminderId/OccurrenceId.</summary>
public readonly record struct BuzzId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
