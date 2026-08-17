using BuzzMe.Api.Mapping;
using BuzzMe.Application.Auth;
using BuzzMe.Contracts.V1.Auth;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Domain.SeedWork;
using FluentValidation;
using FluentValidation.Results;

namespace BuzzMe.Api.Endpoints;

/// <summary>
/// API_CONTRACT.md §5 — the six explicitly unauthenticated Auth endpoints (§2's own list).
/// No `.RequireAuthorization()` group, unlike every other endpoint file — these are the
/// endpoints that establish authentication in the first place. DeleteAccount is not here:
/// it needs Board ownership reassignment, which no sprint has built (SPRINT_9_REPORT.md).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/verify", VerifyAccountAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh-token", RefreshTokenAsync);
        group.MapPost("/forgot-password", ForgotPasswordAsync);
        group.MapPost("/reset-password", ResetPasswordAsync);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, IValidator<RegisterRequest> validator, AuthApplicationService authService, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailure<RegisterResponse>(validation);

        var result = await authService.RegisterAsync(request.Email, request.Phone, request.Password, request.DisplayName, cancellationToken);
        if (result.IsFailure)
            return Failure<RegisterResponse>(result.Error);

        return Results.Json(
            new ApiResponse<RegisterResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> VerifyAccountAsync(
        VerifyAccountRequest request, IValidator<VerifyAccountRequest> validator, AuthApplicationService authService, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailure<AuthResponse>(validation);

        var result = await authService.VerifyAccountAsync(request.Email, request.Phone, request.Code, cancellationToken);
        if (result.IsFailure)
            return Failure<AuthResponse>(result.Error);

        return Results.Json(new ApiResponse<AuthResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, IValidator<LoginRequest> validator, AuthApplicationService authService, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailure<AuthResponse>(validation);

        var result = await authService.LoginAsync(request.Email, request.Phone, request.Password, cancellationToken);
        if (result.IsFailure)
            return Failure<AuthResponse>(result.Error);

        return Results.Json(new ApiResponse<AuthResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> RefreshTokenAsync(
        RefreshTokenRequest request, IValidator<RefreshTokenRequest> validator, AuthApplicationService authService, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailure<TokenPairResponse>(validation);

        var result = await authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (result.IsFailure)
            return Failure<TokenPairResponse>(result.Error);

        return Results.Json(new ApiResponse<TokenPairResponse>(result.Value.ToResponse(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request, IValidator<ForgotPasswordRequest> validator, AuthApplicationService authService, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailure<object>(validation);

        // API_CONTRACT.md §5: "always this response, whether or not the account exists" — there is no failure branch to check.
        await authService.ForgotPasswordAsync(request.Email, request.Phone, cancellationToken);

        return Results.Json(new ApiResponse<object>(new object(), null), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request, IValidator<ResetPasswordRequest> validator, AuthApplicationService authService, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailure<object>(validation);

        var result = await authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
        if (result.IsFailure)
            return Failure<object>(result.Error);

        return Results.Json(new ApiResponse<object>(new object(), null), statusCode: StatusCodes.Status200OK);
    }

    private static IResult ValidationFailure<TData>(ValidationResult validation)
    {
        var validationError = new ApiError(
            ErrorCode.ValidationError, "Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList());
        return Results.Json(new ApiResponse<TData>(default, validationError), statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult Failure<TData>(Error error)
    {
        var (statusCode, apiError) = error.ToHttp();
        return Results.Json(new ApiResponse<TData>(default, apiError), statusCode: statusCode);
    }
}
