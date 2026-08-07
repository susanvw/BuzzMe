namespace BuzzMe.Domain.Occurrences;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — the full lifecycle (`Scheduled → Due → (Completed | Dismissed | Missed)`).
/// Sprint 3 only ever constructs `Scheduled` — nothing exists yet to transition it further
/// (that's completion/Workers scope) — but the complete, already-specified enum is modeled
/// now rather than a partial one, since the values themselves aren't speculative.
/// </summary>
public enum OccurrenceStatus
{
    Scheduled,
    Due,
    Completed,
    Dismissed,
    Missed,
}

/// <summary>Canonical short-codes, same reasoning as RecurrenceCodes/NotifyPresetCodes — needed for Mongo storage even with no API exposure yet.</summary>
public static class OccurrenceStatusCodes
{
    public static string ToCode(this OccurrenceStatus status) => status switch
    {
        OccurrenceStatus.Scheduled => "scheduled",
        OccurrenceStatus.Due => "due",
        OccurrenceStatus.Completed => "completed",
        OccurrenceStatus.Dismissed => "dismissed",
        OccurrenceStatus.Missed => "missed",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static bool TryParse(string? code, out OccurrenceStatus status)
    {
        switch (code)
        {
            case "scheduled": status = OccurrenceStatus.Scheduled; return true;
            case "due": status = OccurrenceStatus.Due; return true;
            case "completed": status = OccurrenceStatus.Completed; return true;
            case "dismissed": status = OccurrenceStatus.Dismissed; return true;
            case "missed": status = OccurrenceStatus.Missed; return true;
            default: status = default; return false;
        }
    }
}
