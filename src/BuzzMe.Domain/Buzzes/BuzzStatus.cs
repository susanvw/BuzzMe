namespace BuzzMe.Domain.Buzzes;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — the full lifecycle (`Scheduled → Generated →
/// (Delivered | Failed → Retried ... → Exhausted) → (Seen | Dismissed)`). Sprint 4 only
/// ever constructs `Scheduled` — nothing exists yet to deliver, retry, or resolve a Buzz
/// (that's dispatch/delivery scope, explicitly excluded here) — but the complete,
/// already-specified enum is modeled now rather than a partial one, since the values
/// themselves aren't speculative. Same reasoning as OccurrenceStatus (Sprint 3).
/// </summary>
public enum BuzzStatus
{
    Scheduled,
    Generated,
    Delivered,
    Failed,
    Retried,
    Exhausted,
    Seen,
    Dismissed,
}

/// <summary>Canonical short-codes, same reasoning as OccurrenceStatusCodes/RecurrenceCodes — needed for Mongo storage even with no API exposure yet.</summary>
public static class BuzzStatusCodes
{
    public static string ToCode(this BuzzStatus status) => status switch
    {
        BuzzStatus.Scheduled => "scheduled",
        BuzzStatus.Generated => "generated",
        BuzzStatus.Delivered => "delivered",
        BuzzStatus.Failed => "failed",
        BuzzStatus.Retried => "retried",
        BuzzStatus.Exhausted => "exhausted",
        BuzzStatus.Seen => "seen",
        BuzzStatus.Dismissed => "dismissed",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static bool TryParse(string? code, out BuzzStatus status)
    {
        switch (code)
        {
            case "scheduled": status = BuzzStatus.Scheduled; return true;
            case "generated": status = BuzzStatus.Generated; return true;
            case "delivered": status = BuzzStatus.Delivered; return true;
            case "failed": status = BuzzStatus.Failed; return true;
            case "retried": status = BuzzStatus.Retried; return true;
            case "exhausted": status = BuzzStatus.Exhausted; return true;
            case "seen": status = BuzzStatus.Seen; return true;
            case "dismissed": status = BuzzStatus.Dismissed; return true;
            default: status = default; return false;
        }
    }
}
