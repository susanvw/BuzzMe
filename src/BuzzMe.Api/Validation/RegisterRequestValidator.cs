using BuzzMe.Contracts.V1.Auth;
using FluentValidation;

namespace BuzzMe.Api.Validation;

/// <summary>Format validation only — the "at least one of Email/Phone" business invariant is also enforced by User's own constructor; this catches it earlier, with a clearer message, before an aggregate is even built.</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.DisplayName).NotEmpty();
        RuleFor(request => request.Email).EmailAddress().When(request => request.Email is not null);
        RuleFor(request => request.Phone).NotEmpty().When(request => request.Phone is not null);
        RuleFor(request => request.Password).MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(request => request)
            .Must(request => !string.IsNullOrWhiteSpace(request.Email) || !string.IsNullOrWhiteSpace(request.Phone))
            .WithMessage("Either email or phone is required.");
    }
}
