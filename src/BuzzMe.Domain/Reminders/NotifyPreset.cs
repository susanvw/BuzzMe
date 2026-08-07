namespace BuzzMe.Domain.Reminders;

/// <summary>
/// IMPLEMENTATION_SPEC.md §1 — exactly these six values, one per Reminder (see
/// SPRINT_2_REPORT.md's discussion of "NotificationPreset collection" — this remains
/// singular, matching every prior document).
/// </summary>
public enum NotifyPreset
{
    AtTime,
    FifteenMinutesBefore,
    OneHourBefore,
    EightHoursBefore,
    OneDayBefore,
    OneWeekBefore,
}

/// <summary>
/// Several wire codes (API_CONTRACT.md §3: `15MinBefore`, `1HourBefore`, ...) start with a
/// digit, which isn't a legal C# identifier — hence an explicit code table here rather than
/// relying on the enum member names themselves, same reasoning as RecurrenceCodes.
/// </summary>
public static class NotifyPresetCodes
{
    public static string ToCode(this NotifyPreset preset) => preset switch
    {
        NotifyPreset.AtTime => "atTime",
        NotifyPreset.FifteenMinutesBefore => "15MinBefore",
        NotifyPreset.OneHourBefore => "1HourBefore",
        NotifyPreset.EightHoursBefore => "8HoursBefore",
        NotifyPreset.OneDayBefore => "1DayBefore",
        NotifyPreset.OneWeekBefore => "1WeekBefore",
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    public static bool TryParse(string? code, out NotifyPreset preset)
    {
        switch (code)
        {
            case "atTime": preset = NotifyPreset.AtTime; return true;
            case "15MinBefore": preset = NotifyPreset.FifteenMinutesBefore; return true;
            case "1HourBefore": preset = NotifyPreset.OneHourBefore; return true;
            case "8HoursBefore": preset = NotifyPreset.EightHoursBefore; return true;
            case "1DayBefore": preset = NotifyPreset.OneDayBefore; return true;
            case "1WeekBefore": preset = NotifyPreset.OneWeekBefore; return true;
            default: preset = default; return false;
        }
    }
}
