using BuzzMe.Contracts.V1.Reminders;
using BuzzMe.Domain.Reminders;
using FluentValidation;

namespace BuzzMe.Api.Validation;

/// <summary>
/// Format validation only: "Reminder title required" and "Reminder schedule required"
/// (Sprint 2's own validation examples) — a schedule without a valid `recurrence` isn't a
/// schedule at all, so that's where "schedule required" is enforced. No additional
/// validation beyond what's already specified.
/// </summary>
public sealed class CreateReminderRequestValidator : AbstractValidator<CreateReminderRequest>
{
    public CreateReminderRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty();
        RuleFor(request => request.Recurrence).Must(code => RecurrenceCodes.TryParse(code, out _))
            .WithMessage("Recurrence must be one of: once, daily, weekly, monthly, yearly.");
        RuleFor(request => request.NotifyPreset).Must(code => NotifyPresetCodes.TryParse(code, out _))
            .WithMessage("NotifyPreset must be one of the supported preset codes.");
    }
}
