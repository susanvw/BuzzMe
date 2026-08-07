using BuzzMe.Contracts.V1.Invitations;
using BuzzMe.Domain.Invitations;
using FluentValidation;

namespace BuzzMe.Api.Validation;

/// <summary>Format validation only — API_CONTRACT.md §5's own validation row: "Target contact format if `email`/`sms`."</summary>
public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(request => request.Channel).Must(code => InvitationChannelCodes.TryParse(code, out _))
            .WithMessage("Channel must be one of: link, email, sms.");

        RuleFor(request => request.TargetContact).NotEmpty()
            .When(request => request.Channel is "email" or "sms")
            .WithMessage("Target contact is required for the email and sms channels.");
    }
}
