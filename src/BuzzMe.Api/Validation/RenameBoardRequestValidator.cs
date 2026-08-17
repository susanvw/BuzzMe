using BuzzMe.Contracts.V1.Boards;
using FluentValidation;

namespace BuzzMe.Api.Validation;

/// <summary>Format validation only, same reasoning as CreateBoardRequestValidator — "Name required" is APPLICATION_LAYER_SPEC.md §3.4's only stated validation for RenameBoard.</summary>
public sealed class RenameBoardRequestValidator : AbstractValidator<RenameBoardRequest>
{
    public RenameBoardRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty();
    }
}
