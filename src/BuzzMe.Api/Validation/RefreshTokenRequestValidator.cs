using BuzzMe.Contracts.V1.Auth;
using FluentValidation;

namespace BuzzMe.Api.Validation;

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(request => request.RefreshToken).NotEmpty();
    }
}
