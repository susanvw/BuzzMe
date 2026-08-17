using BuzzMe.Contracts.V1.Auth;
using FluentValidation;

namespace BuzzMe.Api.Validation;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
        RuleFor(request => request.NewPassword).MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}
