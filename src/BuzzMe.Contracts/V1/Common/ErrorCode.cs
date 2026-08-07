namespace BuzzMe.Contracts.V1.Common;

/// <summary>
/// The full, closed vocabulary of `error.code` wire values — API_CONTRACT.md §6.
/// Plain string constants, deliberately not an enum: the values on the wire must match
/// this exactly, and a constants class makes that a direct, unconfigured fact rather than
/// something that depends on a JSON naming-policy convention.
/// </summary>
public static class ErrorCode
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string Gone = "GONE";
    public const string RateLimited = "RATE_LIMITED";
    public const string ServerError = "SERVER_ERROR";
}
