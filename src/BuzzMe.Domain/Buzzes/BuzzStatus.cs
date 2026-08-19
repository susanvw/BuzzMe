namespace BuzzMe.Domain.Buzzes;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — the lifecycle (`Scheduled → Generated →
/// (Delivered | Failed → Retried ... → Exhausted) → (Seen | Dismissed)`), plus
/// <see cref="Cancelled"/> (Sprint 17) — not part of that original enumeration, but
/// required by the "cancel pending Buzzes" policy IMPLEMENTATION_SPEC.md §4/§7 names
/// repeatedly and by name, for which no other status value fits. Retried/Exhausted/Seen/
/// Dismissed remain unreachable in this codebase — nothing yet transitions a Buzz into any
/// of them (retry scheduling and in-app notification read-tracking are both still
/// unbuilt) — modeled anyway since the values themselves aren't speculative.
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
    Cancelled,
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
        BuzzStatus.Cancelled => "cancelled",
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
            case "cancelled": status = BuzzStatus.Cancelled; return true;
            default: status = default; return false;
        }
    }
}
