using BuzzMe.Api.Mapping;
using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Users;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Users;
using FluentValidation;

namespace BuzzMe.Api.Endpoints;

/// <summary>
/// API_CONTRACT.md §5 — exactly the two already-specified, buildable endpoints (Get
/// Current User, Update Profile). No `POST /auth/register`: that route is specified as
/// unauthenticated and requires password storage and a verification-code pipeline this
/// codebase doesn't have — see SPRINT_8_REPORT.md's specification gap.
/// `UserApplicationService.ProvisionAccountAsync` exists and is fully tested, same
/// "capability exists, no API surface yet" posture as Sprint 3/4/5's internal-only
/// methods — no endpoint invented for it.
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/users").RequireAuthorization();

        group.MapGet("/me", GetCurrentUserAsync);
        group.MapPatch("/me", UpdateProfileAsync);

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
}
