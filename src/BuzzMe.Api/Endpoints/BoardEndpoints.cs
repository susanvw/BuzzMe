using BuzzMe.Api.Mapping;
using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Boards;
using BuzzMe.Contracts.V1.Boards;
using BuzzMe.Contracts.V1.Common;
using FluentValidation;

namespace BuzzMe.Api.Endpoints;

/// <summary>API_CONTRACT.md §5 — Board APIs: Create, Get, List, Mute/Unmute (Sprint 7), and (Sprint 10) Leave/Remove Member.</summary>
public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/boards").RequireAuthorization();

        group.MapPost("", CreateBoardAsync);
        group.MapGet("/{boardId:guid}", GetBoardAsync);
        group.MapGet("", ListBoardsAsync);
        group.MapPost("/{boardId:guid}/mute", MuteBoardAsync);
        group.MapPost("/{boardId:guid}/unmute", UnmuteBoardAsync);
        group.MapPost("/{boardId:guid}/leave", LeaveBoardAsync);
        group.MapDelete("/{boardId:guid}/members/{userId:guid}", RemoveMemberAsync);

        return app;
    }

    private static async Task<IResult> CreateBoardAsync(
        CreateBoardRequest request,
        IValidator<CreateBoardRequest> validator,
        BoardApplicationService boardService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var validationError = new ApiError(
                ErrorCode.ValidationError, "Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList());
            return Results.Json(new ApiResponse<BoardResponse>(null, validationError), statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await boardService.CreateBoardAsync(currentUser.UserId, request.Name, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<BoardResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(
            new ApiResponse<BoardResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetBoardAsync(
        Guid boardId,
        BoardApplicationService boardService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var result = await boardService.GetBoardAsync(currentUser.UserId, boardId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<BoardResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(new ApiResponse<BoardResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> ListBoardsAsync(
        string? cursor,
        int? limit,
        BoardApplicationService boardService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        // API_CONTRACT.md §7 — default 20, max 100.
        var effectiveLimit = Math.Clamp(limit ?? 20, 1, 100);

        var result = await boardService.ListBoardsAsync(currentUser.UserId, cursor, effectiveLimit, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiListResponse<BoardResponse>(null, null, apiError), statusCode: statusCode);
        }

        return Results.Json(result.Value.ToListResponse(), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> MuteBoardAsync(
        Guid boardId, BoardApplicationService boardService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await boardService.MuteBoardAsync(currentUser.UserId, boardId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<object>(null, apiError), statusCode: statusCode);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> UnmuteBoardAsync(
        Guid boardId, BoardApplicationService boardService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await boardService.UnmuteBoardAsync(currentUser.UserId, boardId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<object>(null, apiError), statusCode: statusCode);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> LeaveBoardAsync(
        Guid boardId, BoardApplicationService boardService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await boardService.LeaveBoardAsync(currentUser.UserId, boardId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<LeaveBoardResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(
            new ApiResponse<LeaveBoardResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid boardId, Guid userId, BoardApplicationService boardService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await boardService.RemoveMemberAsync(currentUser.UserId, boardId, userId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<object>(null, apiError), statusCode: statusCode);
        }

        return Results.NoContent();
    }
}
