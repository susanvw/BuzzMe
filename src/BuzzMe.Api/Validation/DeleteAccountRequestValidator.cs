using BuzzMe.Contracts.V1.Auth;
using FluentValidation;

namespace BuzzMe.Api.Validation;

/// <summary>`confirmation` must be explicitly `true` — API_CONTRACT.md §5's `{ confirmation: true }`; omitting it or sending `false` is a format/shape rejection, not a business one.</summary>
public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(request => request.Confirmation).Equal(true).WithMessage("Confirmation is required.");
    }
}
