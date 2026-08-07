using BuzzMe.Contracts.V1.Common;
using BuzzMe.Domain.SeedWork;

namespace BuzzMe.Api.Mapping;

/// <summary>
/// The one place a Domain <see cref="Error"/> becomes an HTTP status code and a Contracts
/// <see cref="ApiError"/> — API_CONTRACT.md §6, shared by every endpoint rather than
/// duplicated per resource. This is the single, narrow, intentional exception to
/// "Api never references Domain" (DEVELOPMENT_GUIDE.md §2/§4): Result&lt;T&gt;/Error are
/// generic outcome plumbing declared in Domain/SeedWork specifically so they can flow all
/// the way to this boundary — not a business object, so referencing them here doesn't
/// violate the rule's intent (an endpoint still never touches an aggregate directly).
/// </summary>
public static class ErrorMapping
{
    public static (int StatusCode, ApiError ApiError) ToHttp(this Error error) => error.Code switch
    {
        ErrorCode.ValidationError => (StatusCodes.Status400BadRequest, new ApiError(error.Code, error.Message)),
        ErrorCode.Unauthorized => (StatusCodes.Status401Unauthorized, new ApiError(error.Code, error.Message)),
        ErrorCode.Forbidden => (StatusCodes.Status403Forbidden, new ApiError(error.Code, error.Message)),
        ErrorCode.NotFound => (StatusCodes.Status404NotFound, new ApiError(error.Code, error.Message)),
        ErrorCode.Conflict => (StatusCodes.Status409Conflict, new ApiError(error.Code, error.Message)),
        ErrorCode.Gone => (StatusCodes.Status410Gone, new ApiError(error.Code, error.Message)),
        _ => (StatusCodes.Status500InternalServerError, new ApiError(ErrorCode.ServerError, "Something went wrong on our end.")),
    };
}
