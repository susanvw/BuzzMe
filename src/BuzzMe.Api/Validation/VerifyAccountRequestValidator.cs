using BuzzMe.Contracts.V1.Auth;
using FluentValidation;

namespace BuzzMe.Api.Validation;

public sealed class VerifyAccountRequestValidator : AbstractValidator<VerifyAccountRequest>
{
    public VerifyAccountRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty();

        RuleFor(request => request)
            .Must(request => !string.IsNullOrWhiteSpace(request.Email) || !string.IsNullOrWhiteSpace(request.Phone))
            .WithMessage("Either email or phone is required.");
    }
}
