namespace BuzzMe.Domain.Reminders;

/// <summary>IMPLEMENTATION_SPEC.md §1 — exactly these five values, no custom rule engine.</summary>
public enum Recurrence
{
    Once,
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

/// <summary>
/// The canonical short-code each value is known by everywhere outside the Domain
/// (API_CONTRACT.md §3's `recurrence` field, the `reminders` Mongo collection). Defined
/// once, here, so Infrastructure's Mapper and the Api layer's mapping code both convert
/// against the same table instead of each maintaining their own — see MembershipRole's
/// precedent from Sprint 1 for why this is an explicit mapping rather than relying on
/// enum-serialization conventions.
/// </summary>
public static class RecurrenceCodes
{
    public static string ToCode(this Recurrence recurrence) => recurrence switch
    {
        Recurrence.Once => "once",
        Recurrence.Daily => "daily",
        Recurrence.Weekly => "weekly",
        Recurrence.Monthly => "monthly",
        Recurrence.Yearly => "yearly",
        _ => throw new ArgumentOutOfRangeException(nameof(recurrence)),
    };

    public static bool TryParse(string? code, out Recurrence recurrence)
    {
        switch (code)
        {
            case "once": recurrence = Recurrence.Once; return true;
            case "daily": recurrence = Recurrence.Daily; return true;
            case "weekly": recurrence = Recurrence.Weekly; return true;
            case "monthly": recurrence = Recurrence.Monthly; return true;
            case "yearly": recurrence = Recurrence.Yearly; return true;
            default: recurrence = default; return false;
        }
    }
}
