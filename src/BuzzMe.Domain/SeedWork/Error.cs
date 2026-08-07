namespace BuzzMe.Domain.SeedWork;

/// <summary>
/// A named, expected failure — never an exception. <see cref="Code"/> is the vocabulary the
/// Api layer maps onto the standard error envelope's `code` field (API_CONTRACT.md §6);
/// domain and application code should never need to know an HTTP status code exists.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string message) => new("VALIDATION_ERROR", message);

    public static Error Unauthorized(string message) => new("UNAUTHORIZED", message);

    public static Error Forbidden(string message) => new("FORBIDDEN", message);

    public static Error NotFound(string message) => new("NOT_FOUND", message);

    public static Error Conflict(string message) => new("CONFLICT", message);

    public static Error Gone(string message) => new("GONE", message);
}
