using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users.Events;

namespace BuzzMe.Domain.Users;

/// <summary>
/// Identity, credential/authentication state, minimal profile, and the reference to its
/// own Personal Board — IMPLEMENTATION_SPEC.md §1's User responsibilities. Root aggregate,
/// no parent. Sprint 9 supersedes Sprint 8's <c>Provision</c> shortcut (see git history)
/// with the real, two-phase Register → Verify flow IMPLEMENTATION_SPEC.md §2 and
/// APPLICATION_LAYER_SPEC.md §3.10 actually specify. <see cref="PersonalBoardId"/> is
/// therefore nullable now — IMPLEMENTATION_SPEC.md §1: "exactly one personalBoardId set
/// exactly once, at account provisioning" — provisioning happens after verification, not
/// at registration (see SPRINT_9_REPORT.md's contradiction note against Domain Model's
/// "created atomically with the account").
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public DisplayName DisplayName { get; private set; }

    public string? PhotoUrl { get; private set; }

    /// <summary>Never a plaintext password — always the output of Application's IPasswordHasher.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>Set exactly once, at Account Provisioning (following VerifyAccount), never changed afterward by any command — IMPLEMENTATION_SPEC.md §1's own stated invariant.</summary>
    public BoardId? PersonalBoardId { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Set by Register, cleared by Verify — IMPLEMENTATION_SPEC.md §2's VerifyAccount precondition: "a valid, unexpired verification code exists for this account."</summary>
    public string? VerificationCode { get; private set; }

    public DateTimeOffset? VerificationCodeExpiresAt { get; private set; }

    /// <summary>A hash of the bearer token, never the plaintext — same at-rest discipline as RefreshToken. Overwritten (never stacked) by each new RequestAccountRecovery, per APPLICATION_LAYER_SPEC.md §3.10's stated idempotency rule.</summary>
    public string? PasswordResetTokenHash { get; private set; }

    public DateTimeOffset? PasswordResetTokenExpiresAt { get; private set; }

    private User(string? email, string? phone, DisplayName displayName, string? photoUrl, string passwordHash, BoardId? personalBoardId)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("A User must have at least one of Email or Phone.");

        Email = email;
        Phone = phone;
        DisplayName = displayName;
        PhotoUrl = photoUrl;
        PasswordHash = passwordHash;
        PersonalBoardId = personalBoardId;
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's RegisterAccount — always starts `PendingVerification`,
    /// with no Personal Board yet. <paramref name="id"/> is server-generated here (via
    /// IIdGenerator, at the Application layer): unlike Sprint 8's Provision, there is no
    /// authenticated caller yet for this identity to be sourced from — Register is the step
    /// that creates the identity in the first place.
    /// </summary>
    public static User Register(
        UserId id, string? email, string? phone, string passwordHash, DisplayName displayName,
        string verificationCode, DateTimeOffset verificationCodeExpiresAt, DateTimeOffset registeredAt)
    {
        var user = new User(email, phone, displayName, photoUrl: null, passwordHash, personalBoardId: null)
        {
            Id = id,
            Status = UserStatus.PendingVerification,
            CreatedAt = registeredAt,
            VerificationCode = verificationCode,
            VerificationCodeExpiresAt = verificationCodeExpiresAt,
        };

        user.Raise(new AccountRegistered(Guid.CreateVersion7(), registeredAt, id));

        return user;
    }

    /// <summary>The exact check IMPLEMENTATION_SPEC.md §2 names as VerifyAccount's precondition — callers check this before calling <see cref="Verify"/>, matching Invitation.IsExpired's placement.</summary>
    public bool HasValidVerificationCode(string code, DateTimeOffset now) =>
        Status == UserStatus.PendingVerification
        && VerificationCode == code
        && VerificationCodeExpiresAt is { } expiresAt
        && now < expiresAt;

    /// <summary>
    /// A pure state transition — the caller (Application layer) is responsible for having
    /// already confirmed <see cref="HasValidVerificationCode"/> and for the already-Active
    /// idempotency short-circuit (APPLICATION_LAYER_SPEC.md §3.10: "re-verifying an already-
    /// Active account is a no-op"), same division of responsibility as
    /// Invitation.Accept/EnsurePending.
    /// </summary>
    public void Verify(DateTimeOffset verifiedAt)
    {
        if (Status != UserStatus.PendingVerification)
            throw new InvalidOperationException($"User {Id} is not PendingVerification (current status: {Status}).");

        Status = UserStatus.Active;
        VerificationCode = null;
        VerificationCodeExpiresAt = null;

        Raise(new AccountVerified(Guid.CreateVersion7(), verifiedAt, Id));
    }

    /// <summary>
    /// The Account Provisioning policy's sole effect on User itself (APPLICATION_LAYER_SPEC.md
    /// §7) — creating the Personal Board is the Application layer's job (a different
    /// aggregate root); this just records the reference, exactly once. Idempotent: a retried
    /// provisioning step against an already-provisioned account is a no-op, matching
    /// IMPLEMENTATION_SPEC.md §4's own stated idempotency rule for this policy.
    /// </summary>
    public void CompleteProvisioning(BoardId personalBoardId)
    {
        if (PersonalBoardId is not null)
            return;

        PersonalBoardId = personalBoardId;
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's RequestAccountRecovery — precondition is explicitly
    /// "None (always accepted, to avoid confirming account existence)," so this never
    /// guards on Status. Overwriting any prior token unconditionally is what makes "a
    /// second request invalidates any prior outstanding token" true without extra bookkeeping.
    /// </summary>
    public void RequestPasswordReset(string tokenHash, DateTimeOffset expiresAt, DateTimeOffset requestedAt)
    {
        PasswordResetTokenHash = tokenHash;
        PasswordResetTokenExpiresAt = expiresAt;

        Raise(new AccountRecoveryRequested(Guid.CreateVersion7(), requestedAt, Id));
    }

    public bool HasValidPasswordResetToken(string tokenHash, DateTimeOffset now) =>
        PasswordResetTokenHash == tokenHash
        && PasswordResetTokenExpiresAt is { } expiresAt
        && now < expiresAt;

    /// <summary>Consumes the reset token so it can never work twice (API_CONTRACT.md §5: "a used token must not work twice") — the caller confirms <see cref="HasValidPasswordResetToken"/> first.</summary>
    public void ResetPassword(string newPasswordHash, DateTimeOffset resetAt)
    {
        if (PasswordResetTokenHash is null)
            throw new InvalidOperationException($"User {Id} has no outstanding password reset token.");

        PasswordHash = newPasswordHash;
        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAt = null;

        Raise(new AccountRecovered(Guid.CreateVersion7(), resetAt, Id));
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.10 — each parameter left `null` means "leave unchanged"
    /// (PATCH semantics); there is no way to explicitly clear Email/Phone to null through
    /// this method, which is exactly what keeps "at least one of Email or Phone" true for
    /// the aggregate's whole lifetime without an extra guard here. Natural idempotency: no
    /// actual change means no event raised, matching §3.10's own stated rule ("re-applying
    /// identical values is a no-op").
    /// </summary>
    public void UpdateProfile(DisplayName? displayName, string? photoUrl, string? email, string? phone, DateTimeOffset updatedAt)
    {
        var changed = false;

        if (displayName is not null && displayName != DisplayName)
        {
            DisplayName = displayName;
            changed = true;
        }

        if (photoUrl is not null && photoUrl != PhotoUrl)
        {
            PhotoUrl = photoUrl;
            changed = true;
        }

        if (email is not null && email != Email)
        {
            Email = email;
            changed = true;
        }

        if (phone is not null && phone != Phone)
        {
            Phone = phone;
            changed = true;
        }

        if (!changed)
            return;

        Raise(new ProfileUpdated(Guid.CreateVersion7(), updatedAt, Id));
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's ConfirmAccountDeletion — the terminal transition; "any
    /// non-Deleted state → Deleted" (UserStatus.cs's own lifecycle comment). A pure state
    /// transition, same division of responsibility as Verify: the Application layer's
    /// DeleteAccountAsync owns the multi-aggregate orchestration this triggers (per-Board
    /// ReassignOwnership/removal, session revocation — APPLICATION_LAYER_SPEC.md §7) and the
    /// already-Deleted idempotency short-circuit ("re-confirming an already-Deleted account
    /// is a no-op") before ever calling this.
    /// </summary>
    public void Delete(DateTimeOffset deletedAt)
    {
        if (Status == UserStatus.Deleted)
            return;

        Status = UserStatus.Deleted;

        Raise(new AccountDeleted(Guid.CreateVersion7(), deletedAt, Id));
    }

    internal static User Rehydrate(
        UserId id, string? email, string? phone, DisplayName displayName, string? photoUrl, string passwordHash,
        BoardId? personalBoardId, UserStatus status, DateTimeOffset createdAt,
        string? verificationCode, DateTimeOffset? verificationCodeExpiresAt,
        string? passwordResetTokenHash, DateTimeOffset? passwordResetTokenExpiresAt, long version)
    {
        var user = new User(email, phone, displayName, photoUrl, passwordHash, personalBoardId)
        {
            Id = id,
            Status = status,
            CreatedAt = createdAt,
            VerificationCode = verificationCode,
            VerificationCodeExpiresAt = verificationCodeExpiresAt,
            PasswordResetTokenHash = passwordResetTokenHash,
            PasswordResetTokenExpiresAt = passwordResetTokenExpiresAt,
        };
        user.Version = version;
        return user;
    }
}
