using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Users.Models;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Users;

/// <summary>
/// One Application Service for the Users bounded-context area — profile read/update, self
/// only. Account creation (Register/VerifyAccount) and credential management (Login,
/// RefreshToken, ForgotPassword/ResetPassword) moved to AuthApplicationService in Sprint 9,
/// which supersedes Sprint 8's ProvisionAccountAsync shortcut — see SPRINT_9_REPORT.md.
/// </summary>
public sealed class UserApplicationService(IUserRepository userRepository, IClock clock)
{
    /// <summary>API_CONTRACT.md §5 — Authorization: Authenticated User, self only.</summary>
    public async Task<Result<UserResult>> GetCurrentUserAsync(Guid requestingUserId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(new UserId(requestingUserId), cancellationToken);
        if (user is null)
            return Result.Failure<UserResult>(Error.NotFound("User not found."));

        return Result.Success(UserResult.FromDomain(user));
    }

    /// <summary>
    /// APPLICATION_LAYER_SPEC.md §3.10 — Authorization: Authenticated User, self only.
    /// Email/phone change does NOT re-trigger verification here (documented specification
    /// gap — no verification pipeline exists in this codebase), but the uniqueness
    /// invariant is still enforced, same as it is at provisioning.
    /// </summary>
    public async Task<Result<UserResult>> UpdateProfileAsync(
        Guid requestingUserId, string? displayName, string? photoUrl, string? email, string? phone, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(new UserId(requestingUserId), cancellationToken);
        if (user is null)
            return Result.Failure<UserResult>(Error.NotFound("User not found."));

        var emailOrPhoneChanging = (email is not null && email != user.Email) || (phone is not null && phone != user.Phone);
        if (emailOrPhoneChanging && await userRepository.ExistsWithEmailOrPhoneAsync(email, phone, user.Id, cancellationToken))
            return Result.Failure<UserResult>(Error.Conflict("Email or phone is already in use by another account."));

        user.UpdateProfile(displayName is null ? null : new DisplayName(displayName), photoUrl, email, phone, clock.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        return Result.Success(UserResult.FromDomain(user));
    }
}
