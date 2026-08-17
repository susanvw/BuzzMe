using BuzzMe.Api.Mapping;
using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Auth;
using BuzzMe.Application.Users;
using BuzzMe.Contracts.V1.Auth;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Users;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BuzzMe.Api.Endpoints;

/// <summary>
/// API_CONTRACT.md §5 — Get Current User, Update Profile, and (Sprint 12) Delete Account.
/// Delete Account calls AuthApplicationService, not UserApplicationService — DeleteAccount
/// is grouped with the rest of the account-lifecycle use cases (Register/Verify/Login/...)
/// there, per Sprint 9's own split, even though its HTTP route lives under `/users/me`
/// alongside Profile's two endpoints (a REST-resource-path choice, not an implementation
/// one — API_CONTRACT.md §1 Principle 3).
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/users").RequireAuthorization();

        group.MapGet("/me", GetCurrentUserAsync);
        group.MapPatch("/me", UpdateProfileAsync);
        group.MapDelete("/me", DeleteAccountAsync);

        return app;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        UserApplicationService userService, ICurrentUserContext currentUser, CancellationToken cancellationToken)
    {
        var result = await userService.GetCurrentUserAsync(currentUser.UserId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<UserResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(new ApiResponse<UserResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        IValidator<UpdateProfileRequest> validator,
        UserApplicationService userService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var validationError = new ApiError(
                ErrorCode.ValidationError, "Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList());
            return Results.Json(new ApiResponse<UserResponse>(null, validationError), statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await userService.UpdateProfileAsync(
            currentUser.UserId, request.DisplayName, request.PhotoUrl, request.Email, request.Phone, cancellationToken);

        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<UserResponse>(null, apiError), statusCode: statusCode);
        }

        return Results.Json(new ApiResponse<UserResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> DeleteAccountAsync(
        [FromBody] DeleteAccountRequest request,
        IValidator<DeleteAccountRequest> validator,
        AuthApplicationService authService,
        ICurrentUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var validationError = new ApiError(
                ErrorCode.ValidationError, "Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList());
            return Results.Json(new ApiResponse<object>(null, validationError), statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await authService.DeleteAccountAsync(currentUser.UserId, cancellationToken);
        if (result.IsFailure)
        {
            var (statusCode, apiError) = result.Error.ToHttp();
            return Results.Json(new ApiResponse<object>(null, apiError), statusCode: statusCode);
        }

        return Results.NoContent();
    }
}
