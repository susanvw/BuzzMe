using MongoDB.Bson.Serialization.Attributes;

namespace BuzzMe.Infrastructure.Persistence.Mongo.Users;

/// <summary>The `users` collection's document shape — one aggregate, one document (DEVELOPMENT_GUIDE.md §6).</summary>
public sealed class UserDocument
{
    [BsonId]
    public required Guid Id { get; init; }

    // BsonIgnoreIfNull, not just `string?` — a sparse index only skips a document that is
    // missing the field entirely. Without this attribute the driver still writes an
    // explicit `Email: null`/`Phone: null`, which the sparse index treats as a real
    // (colliding) value across every User that has neither set (Sprint 8's own lesson,
    // applied here up front for the two new sparse-indexed fields below).
    [BsonIgnoreIfNull]
    public string? Email { get; init; }

    [BsonIgnoreIfNull]
    public string? Phone { get; init; }

    public required string DisplayName { get; init; }

    public string? PhotoUrl { get; init; }

    public required string PasswordHash { get; init; }

    /// <summary>Null until Account Provisioning runs, following VerifyAccount — IMPLEMENTATION_SPEC.md §1.</summary>
    public Guid? PersonalBoardId { get; init; }

    /// <summary>Stored as a string, not the C# enum — same reasoning as every other Status field in this codebase.</summary>
    public required string Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    [BsonIgnoreIfNull]
    public string? VerificationCode { get; init; }

    [BsonIgnoreIfNull]
    public DateTimeOffset? VerificationCodeExpiresAt { get; init; }

    /// <summary>Sparse-indexed (ResetPasswordAsync looks a User up by this) — BsonIgnoreIfNull for the same reason as Email/Phone above.</summary>
    [BsonIgnoreIfNull]
    public string? PasswordResetTokenHash { get; init; }

    [BsonIgnoreIfNull]
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; init; }

    public required long Version { get; init; }
}
