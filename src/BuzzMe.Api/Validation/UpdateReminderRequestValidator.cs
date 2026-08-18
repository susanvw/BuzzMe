using BuzzMe.Contracts.V1.Reminders;
using BuzzMe.Domain.Reminders;
using FluentValidation;

namespace BuzzMe.Api.Validation;

/// <summary>
/// Format validation only, same rules as CreateReminderRequestValidator but each field is
/// optional — "Same field validation as Create" (API_CONTRACT.md §5's Update Reminder row),
/// applied only to whichever fields the caller actually sent.
/// </summary>
public sealed class UpdateReminderRequestValidator : AbstractValidator<UpdateReminderRequest>
{
    public UpdateReminderRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().When(request => request.Title is not null);
        RuleFor(request => request.Recurrence).Must(code => RecurrenceCodes.TryParse(code, out _))
            .When(request => request.Recurrence is not null)
            .WithMessage("Recurrence must be one of: once, daily, weekly, monthly, yearly.");
        RuleFor(request => request.NotifyPreset).Must(code => NotifyPresetCodes.TryParse(code, out _))
            .When(request => request.NotifyPreset is not null)
            .WithMessage("NotifyPreset must be one of the supported preset codes.");
    }
}
