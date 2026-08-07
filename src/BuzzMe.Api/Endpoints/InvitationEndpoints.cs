using BuzzMe.Api.Mapping;
using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Invitations;
using BuzzMe.Contracts.V1.Boards;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Invitations;
using FluentValidation;

namespace BuzzMe.Api.Endpoints;

/// <summary>
/// API_CONTRACT.md §5 — exactly the four already-specified endpoints (Invite Member,
/// Validate, Accept, Decline). No Cancel endpoint: not defined anywhere in API_CONTRACT.md
/// — see SPRINT_5_REPORT.md's specification gap. That Application capability still exists
/// (InvitationApplicationService.CancelInvitationAsync), same posture as Sprint 3/4's
/// read-only Occurrence/Buzz methods — no API surface yet. (`ListPendingInvitationsAsync`,
/// the other capability with no endpoint, was removed entirely in Sprint 6 — it had no
/// specification basis at all, unlike Cancel's — see SPRINT_6_REPORT.md.)
/// </summary>
public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/v1/boards/{boardId:guid}/invitations")
            .RequireAuthorization()
            .MapPost("", InviteMemberAsync);

        var invitations = app.MapGroup("/v1/invitations");

        // Public — API_CONTRACT.md §2: must work for someone with no account yet.
        invitations.MapGet("/{token}", ValidateInvitationAsync);

        invitations.MapPost("/{token}/accept", AcceptInvitationAsync).RequireAuthorization();
        invitations.MapPost("/{token}/decline", DeclineInvitationAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> InviteMemberAsync(
        Guid boardId,
        InviteMemberRequest request,
        IValidator<InviteMemberRequest> validator,
        InvitationApplicationService invitationService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var validationError = new ApiError(
                ErrorCode.ValidationError, "Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList());
            return Results.Json(new ApiResponse<InvitationResponse>(null, validationError), statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await invitationService.InviteMemberAsync(
            currentUser.UserId, boardId, request.Channel, request.TargetContact, cancellationToken);

        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<InvitationResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(
            new ApiResponse<InvitationResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ValidateInvitationAsync(
        string token, InvitationApplicationService invitationService, CancellationToken cancellationToken)
    {
        var result = await invitationService.ValidateInvitationAsync(token, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<ValidateInvitationResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(
            new ApiResponse<ValidateInvitationResponse>(result.Value.ToValidateResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> AcceptInvitationAsync(
        string token, InvitationApplicationService invitationService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await invitationService.AcceptInvitationAsync(currentUser.UserId, token, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<MembershipResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(new ApiResponse<MembershipResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> DeclineInvitationAsync(
        string token, InvitationApplicationService invitationService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await invitationService.DeclineInvitationAsync(currentUser.UserId, token, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<DeclineInvitationResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(
            new ApiResponse<DeclineInvitationResponse>(new DeclineInvitationResponse("declined"), null), statusCode: StatusCodes.Status200OK);
    }
}
