using BuzzMe.Api.Mapping;
using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Occurrences;
using BuzzMe.Application.Occurrences.Models;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Occurrences;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Api.Endpoints;

/// <summary>
/// API_CONTRACT.md §5's Complete/Dismiss/Reopen Reminder row (Sprint 15) — the three
/// Occurrence-resolution actions. Nested two levels deep under Reminder in the URL
/// (`/reminders/{reminderId}/occurrences/{occurrenceId}/...`) per API_CONTRACT.md §1's own
/// note that this is a deliberate case where the API shape does not mirror the Occurrence
/// aggregate boundary 1:1 — an Occurrence has no meaning to a client independent of its
/// Reminder. No plain Get/List Occurrence endpoints exist in API_CONTRACT.md — only these
/// three actions do.
/// </summary>
public static class OccurrenceEndpoints
{
    public static IEndpointRouteBuilder MapOccurrenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/reminders/{reminderId:guid}/occurrences/{occurrenceId:guid}").RequireAuthorization();

        group.MapPost("/complete", CompleteOccurrenceAsync);
        group.MapPost("/dismiss", DismissOccurrenceAsync);
        group.MapPost("/reopen", ReopenOccurrenceAsync);

        return app;
    }

    private static async Task<IResult> CompleteOccurrenceAsync(
        Guid reminderId,
        Guid occurrenceId,
        ResolveOccurrenceRequest request,
        OccurrenceApplicationService occurrenceService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var result = await occurrenceService.CompleteOccurrenceAsync(
            currentUser.UserId, reminderId, occurrenceId, request.ExpectedVersion, cancellationToken);

        return ToResult(result);
    }

    private static async Task<IResult> DismissOccurrenceAsync(
        Guid reminderId,
        Guid occurrenceId,
        ResolveOccurrenceRequest request,
        OccurrenceApplicationService occurrenceService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var result = await occurrenceService.DismissOccurrenceAsync(
            currentUser.UserId, reminderId, occurrenceId, request.ExpectedVersion, cancellationToken);

        return ToResult(result);
    }

    private static async Task<IResult> ReopenOccurrenceAsync(
        Guid reminderId,
        Guid occurrenceId,
        ResolveOccurrenceRequest request,
        OccurrenceApplicationService occurrenceService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var result = await occurrenceService.ReopenOccurrenceAsync(
            currentUser.UserId, reminderId, occurrenceId, request.ExpectedVersion, cancellationToken);

        return ToResult(result);
    }

    /// <summary>
    /// API_CONTRACT.md §5 — 200 for a genuine transition or a same-version idempotent
    /// replay, 409 (with the resolved Occurrence still in the body) for the "already done
    /// by X" version-mismatch race — both are a success at the Result/Error level, per
    /// OccurrenceResolutionResult's own doc comment; only VersionConflict picks the status.
    /// </summary>
    private static IResult ToResult(Result<OccurrenceResolutionResult> result)
    {
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<OccurrenceResponse>(null, apiError), statusCode: statusCode);
        }

        var responseStatusCode = result.Value.VersionConflict ? StatusCodes.Status409Conflict : StatusCodes.Status200OK;
        var conflictError = result.Value.VersionConflict
            ? new ApiError(ErrorCode.Conflict, "This Occurrence was already resolved by someone else.")
            : null;

        return Results.Json(
            new ApiResponse<OccurrenceResponse>(result.Value.Occurrence.ToResponse(), conflictError), statusCode: responseStatusCode);
    }
}
