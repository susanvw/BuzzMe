using BuzzMe.Contracts.V1.Users;
using FluentValidation;

namespace BuzzMe.Api.Validation;

/// <summary>Format validation only — every field is optional (PATCH semantics), but a field that IS provided must not be empty/malformed.</summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.DisplayName).NotEmpty().When(request => request.DisplayName is not null);
        RuleFor(request => request.Email).EmailAddress().When(request => request.Email is not null);
        RuleFor(request => request.Phone).NotEmpty().When(request => request.Phone is not null);
    }
}
