namespace BuzzMe.Domain.Reminders;

/// <summary>
/// "When and how often" as one cohesive value object — IMPLEMENTATION_SPEC.md §1 described
/// `recurrence`, `startDate` (API_CONTRACT.md §5), and `referenceTimezone` as three flat
/// fields; bundling them here is a packaging choice only (Sprint 2's "ReminderSchedule"
/// naming), not a change to what's stored or how the wire contract looks.
///
/// Sprint 3 resolves the timezone gap flagged since the Architecture Review: each
/// Occurrence's due-instant is resolved fresh, per occurrence, from this schedule's local
/// wall-clock pattern against <see cref="ReferenceTimezone"/> — never a fixed offset
/// computed once and reused. See SPRINT_3_REPORT.md §3 for the full decision record,
/// including the two DST edge cases (invalid and ambiguous local times) this was verified
/// against empirically before being implemented.
/// </summary>
public sealed record ReminderSchedule
{
    public Recurrence Recurrence { get; }

    /// <summary>The first occurrence's local wall-clock date/time — Kind is always treated as Unspecified ("floating", no baked-in offset).</summary>
    public DateTime StartDate { get; }

    /// <summary>IANA zone id — IMPLEMENTATION_SPEC.md §1, immutable after creation.</summary>
    public string ReferenceTimezone { get; }

    public ReminderSchedule(Recurrence recurrence, DateTime startDate, string referenceTimezone)
    {
        if (string.IsNullOrWhiteSpace(referenceTimezone))
            throw new ArgumentException("A Reminder's reference timezone cannot be empty.", nameof(referenceTimezone));

        Recurrence = recurrence;
        StartDate = startDate;
        ReferenceTimezone = referenceTimezone;
    }

    /// <summary>
    /// The local wall-clock date/time of the occurrence at <paramref name="occurrenceIndex"/>
    /// (0 = the first occurrence, i.e. <see cref="StartDate"/> itself). Pure calendar
    /// arithmetic — no timezone resolution happens here, so this never throws for a DST
    /// edge case; only <see cref="ResolveDueInstant"/> does that work.
    /// </summary>
    public DateTime GetLocalDateTimeForOccurrence(int occurrenceIndex)
    {
        if (occurrenceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(occurrenceIndex), "Occurrence index cannot be negative.");

        if (Recurrence == Recurrence.Once && occurrenceIndex > 0)
            throw new InvalidOperationException("A one-time Reminder has exactly one occurrence.");

        return Recurrence switch
        {
            Recurrence.Once => StartDate,
            Recurrence.Daily => StartDate.AddDays(occurrenceIndex),
            Recurrence.Weekly => StartDate.AddDays(occurrenceIndex * 7),
            // .NET's AddMonths/AddYears clip an overflowing day-of-month to the target
            // month's last valid day (e.g. Jan 31 + 1 month -> Feb 28) — the standard,
            // widely-expected behavior for calendar-based recurrence, not a workaround.
            Recurrence.Monthly => StartDate.AddMonths(occurrenceIndex),
            Recurrence.Yearly => StartDate.AddYears(occurrenceIndex),
            _ => throw new ArgumentOutOfRangeException(nameof(occurrenceIndex), "Unrecognized recurrence value."),
        };
    }

    /// <summary>
    /// The absolute UTC instant occurrence <paramref name="occurrenceIndex"/> is due at —
    /// resolved fresh against <see cref="ReferenceTimezone"/>'s rules *for that specific
    /// calendar date*, so DST differences between one occurrence and the next are captured
    /// correctly (SPRINT_3_REPORT.md §3).
    ///
    /// Two DST edge cases, both verified empirically against .NET's TimeZoneInfo before
    /// this was written:
    /// - **Ambiguous local time** (the hour repeated during a fall-back transition):
    ///   .NET's TimeZoneInfo.ConvertTimeToUtc resolves this deterministically to the
    ///   post-transition (standard time) interpretation, with no special handling needed.
    /// - **Invalid local time** (the hour skipped during a spring-forward transition):
    ///   .NET throws by default. Resolved here by shifting the local time forward by the
    ///   zone's own DaylightDelta for that date before converting — the same "skip forward
    ///   past the gap" convention most calendar systems use, using the timezone's actual
    ///   adjustment rule rather than an assumed fixed offset.
    /// </summary>
    public DateTimeOffset ResolveDueInstant(int occurrenceIndex)
    {
        var localDateTime = DateTime.SpecifyKind(GetLocalDateTimeForOccurrence(occurrenceIndex), DateTimeKind.Unspecified);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ReferenceTimezone);

        if (timeZone.IsInvalidTime(localDateTime))
        {
            var rule = timeZone.GetAdjustmentRules()
                .FirstOrDefault(r => localDateTime.Date >= r.DateStart && localDateTime.Date <= r.DateEnd);
            var delta = rule?.DaylightDelta ?? TimeSpan.FromHours(1);
            localDateTime = localDateTime.Add(delta);
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
