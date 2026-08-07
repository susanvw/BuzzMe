namespace BuzzMe.Domain.Reminders;

/// <summary>
/// "Reminder title required" (Sprint 2 validation scope). Same pattern as BoardName: the
/// upstream API boundary is the real gate for this (format validation), this constructor is
/// the defensive Domain guard for the case that check somehow didn't run.
/// </summary>
public sealed record ReminderTitle
{
    public string Value { get; }

    public ReminderTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A Reminder's title cannot be empty.", nameof(value));

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
