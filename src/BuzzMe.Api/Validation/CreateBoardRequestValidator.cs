using BuzzMe.Contracts.V1.Boards;
using FluentValidation;

namespace BuzzMe.Api.Validation;

/// <summary>
/// Format validation only ("a title isn't empty" — DEVELOPMENT_GUIDE.md §9's own example).
/// Business validation for CreateBoard is "none beyond the name existing"
/// (APPLICATION_LAYER_SPEC.md §3.1) — there is nothing else for this validator to check.
/// </summary>
public sealed class CreateBoardRequestValidator : AbstractValidator<CreateBoardRequest>
{
    public CreateBoardRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty();
    }
}
