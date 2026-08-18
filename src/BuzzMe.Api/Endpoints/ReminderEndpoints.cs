using System.Text.Json;
using BuzzMe.Api.Mapping;
using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Reminders;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Reminders;
using FluentValidation;

namespace BuzzMe.Api.Endpoints;

/// <summary>
/// API_CONTRACT.md §5 Reminder APIs: Create, Get, List, Delete (Sprint 2), Update (Sprint
/// 16). Create and List are nested under Board (a Reminder always needs a Board to be
/// created against, or to be listed for); Get, Update, and Delete are reached via
/// API_CONTRACT.md's existing flat `/reminders/{reminderId}` path (§1 Principle 3) — see
/// SPRINT_2_REPORT.md for the discovered discrepancy with the sprint brief's own
/// nested-looking phrasing and why the already-established Contract wins.
/// </summary>
public static class ReminderEndpoints
{
    public static IEndpointRouteBuilder MapReminderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/v1/boards/{boardId:guid}/reminders")
            .RequireAuthorization()
            .MapReminderCollectionEndpoints();

        app.MapGroup("/v1/reminders")
            .RequireAuthorization()
            .MapReminderItemEndpoints();

        return app;
    }

    private static void MapReminderCollectionEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapPost("", CreateReminderAsync);
        group.MapGet("", ListBoardRemindersAsync);
    }

    private static void MapReminderItemEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/{reminderId:guid}", GetReminderAsync);
        group.MapPatch("/{reminderId:guid}", UpdateReminderAsync);
        group.MapDelete("/{reminderId:guid}", DeleteReminderAsync);
    }

    private static async Task<IResult> CreateReminderAsync(
        Guid boardId,
        CreateReminderRequest request,
        IValidator<CreateReminderRequest> validator,
        ReminderApplicationService reminderService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var validationError = new ApiError(
                ErrorCode.ValidationError, "Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList());
            return Results.Json(new ApiResponse<ReminderResponse>(null, validationError), statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await reminderService.CreateReminderAsync(
            currentUser.UserId, boardId, request.Title, request.Recurrence, request.StartDate, request.NotifyPreset, cancellationToken);

        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<ReminderResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(
            new ApiResponse<ReminderResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListBoardRemindersAsync(
        Guid boardId,
        string? cursor,
        int? limit,
        ReminderApplicationService reminderService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit ?? 20, 1, 100);

        var result = await reminderService.ListBoardRemindersAsync(currentUser.UserId, boardId, cursor, effectiveLimit, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiListResponse<ReminderResponse>(null, null, apiError), statusCode: statusCode);
        }

        return Results.Json(result.Value.ToListResponse(), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetReminderAsync(
        Guid reminderId, ReminderApplicationService reminderService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await reminderService.GetReminderAsync(currentUser.UserId, reminderId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<ReminderResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(new ApiResponse<ReminderResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    /// <summary>
    /// API_CONTRACT.md §5's Update Reminder row: "`boardId` is not an accepted field;
    /// present in payload → `400`, not silently ignored." UpdateReminderRequest has no
    /// BoardId property at all, so a normal typed body-bind would just drop an extra
    /// `boardId` field silently (System.Text.Json's default behavior for unmapped
    /// members) — the raw body is parsed once here specifically to catch that case
    /// before the typed request is built from the same JSON.
    /// </summary>
    private static async Task<IResult> UpdateReminderAsync(
        Guid reminderId,
        HttpContext httpContext,
        IValidator<UpdateReminderRequest> validator,
        ReminderApplicationService reminderService,
        ICurrentUserContext currentUser,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(httpContext.Request.Body, cancellationToken: cancellationToken);

        if (document.RootElement.EnumerateObject().Any(property => string.Equals(property.Name, "boardId", StringComparison.OrdinalIgnoreCase)))
        {
            var boardIdError = new ApiError(ErrorCode.ValidationError, "boardId is not an accepted field on this request.");
            return Results.Json(new ApiResponse<ReminderResponse>(null, boardIdError), statusCode: StatusCodes.Status400BadRequest);
        }

        var request = document.RootElement.Deserialize<UpdateReminderRequest>(jsonOptions) ?? new UpdateReminderRequest(null, null, null, null);

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var validationError = new ApiError(
                ErrorCode.ValidationError, "Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList());
            return Results.Json(new ApiResponse<ReminderResponse>(null, validationError), statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await reminderService.UpdateReminderAsync(
            currentUser.UserId, reminderId, request.Title, request.Recurrence, request.StartDate, request.NotifyPreset, cancellationToken);

        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<ReminderResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(new ApiResponse<ReminderResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> DeleteReminderAsync(
        Guid reminderId, ReminderApplicationService reminderService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await reminderService.DeleteReminderAsync(currentUser.UserId, reminderId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<object>(null, apiError), statusCode: statusCode);
        }

        return Results.NoContent();
    }
}
