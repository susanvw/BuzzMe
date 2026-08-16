using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users.Events;

namespace BuzzMe.Domain.Users;

/// <summary>
/// Identity, minimal profile, and the reference to its own Personal Board —
/// IMPLEMENTATION_SPEC.md §1's User responsibilities. Root aggregate, no parent. Sprint 8
/// scope only: provisioning (see <see cref="Provision"/> for why this isn't literally
/// `RegisterAccount`/`VerifyAccount`) and profile read/update — no credential storage, no
/// authentication state beyond <see cref="Status"/>, no self-service Deactivate or Delete
/// (see SPRINT_8_REPORT.md's specification gaps).
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public DisplayName DisplayName { get; private set; }

    public string? PhotoUrl { get; private set; }

    /// <summary>Set exactly once, at provisioning, never changed afterward by any command — IMPLEMENTATION_SPEC.md §1's own stated invariant.</summary>
    public BoardId PersonalBoardId { get; private init; }

    public UserStatus Status { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    private User(string? email, string? phone, DisplayName displayName, string? photoUrl, BoardId personalBoardId)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("A User must have at least one of Email or Phone.");

        Email = email;
        Phone = phone;
        DisplayName = displayName;
        PhotoUrl = photoUrl;
        PersonalBoardId = personalBoardId;
    }

    /// <summary>
    /// The only way a new User comes into existence — always starts `Active`, with its
    /// Personal Board already assigned. Deliberately not named `Register` or `Verify`:
    /// this collapses IMPLEMENTATION_SPEC.md's `RegisterAccount` → `VerifyAccount` →
    /// Account Provisioning sequence into one step, because there is no password/
    /// verification-code infrastructure anywhere in this codebase for the real, two-phase
    /// flow to run against — see SPRINT_8_REPORT.md. <paramref name="id"/> is never
    /// generated here; it is always the identity a caller's own JWT already established
    /// (UserApplicationService.ProvisionAccountAsync).
    /// </summary>
    public static User Provision(
        UserId id, string? email, string? phone, DisplayName displayName, BoardId personalBoardId, DateTimeOffset provisionedAt)
    {
        var user = new User(email, phone, displayName, photoUrl: null, personalBoardId)
        {
            Id = id,
            Status = UserStatus.Active,
            CreatedAt = provisionedAt,
        };

        user.Raise(new UserAccountProvisioned(Guid.CreateVersion7(), provisionedAt, id, personalBoardId));

        return user;
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

    internal static User Rehydrate(
        UserId id, string? email, string? phone, DisplayName displayName, string? photoUrl,
        BoardId personalBoardId, UserStatus status, DateTimeOffset createdAt, long version)
    {
        var user = new User(email, phone, displayName, photoUrl, personalBoardId)
        {
            Id = id,
            Status = status,
            CreatedAt = createdAt,
        };
        user.Version = version;
        return user;
    }
}
