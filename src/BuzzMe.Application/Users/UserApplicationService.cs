using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Users.Models;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Users;

/// <summary>
/// One Application Service for the Users bounded-context area — Sprint 8's three use
/// cases. `ProvisionAccountAsync` is deliberately not API_CONTRACT.md's unauthenticated
/// `POST /auth/register` (which needs password storage and a verification-code pipeline
/// this codebase doesn't have — see SPRINT_8_REPORT.md's specification gap); it requires
/// an already-authenticated caller and uses their own JWT-established identity as the new
/// User's Id, never a generated one.
/// </summary>
public sealed class UserApplicationService(
    IUserRepository userRepository, IBoardRepository boardRepository, IIdGenerator idGenerator, IClock clock)
{
    /// <summary>
    /// Collapses IMPLEMENTATION_SPEC.md's RegisterAccount → VerifyAccount → Account
    /// Provisioning sequence into one step (see User.Provision's own doc comment).
    /// Personal Board is created first, so its Id is available to set on the User in the
    /// same operation — matching APPLICATION_LAYER_SPEC.md §7's own stated ordering.
    /// Idempotent: calling this again for an already-provisioned caller returns their
    /// existing record rather than erroring.
    /// </summary>
    public async Task<Result<UserResult>> ProvisionAccountAsync(
        Guid requestingUserId, string? email, string? phone, string displayName, CancellationToken cancellationToken)
    {
        var userId = new UserId(requestingUserId);

        var existing = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (existing is not null)
            return Result.Success(UserResult.FromDomain(existing));

        if (await userRepository.ExistsWithEmailOrPhoneAsync(email, phone, excludingUserId: null, cancellationToken))
            return Result.Failure<UserResult>(Error.Conflict("Email or phone is already registered."));

        var personalBoardId = new BoardId(idGenerator.NewId());
        var personalBoard = Board.Create(personalBoardId, new BoardName("Personal"), requestingUserId, clock.UtcNow);
        await boardRepository.AddAsync(personalBoard, cancellationToken);

        var user = User.Provision(userId, email, phone, new DisplayName(displayName), personalBoardId, clock.UtcNow);
        await userRepository.AddAsync(user, cancellationToken);

        return Result.Success(UserResult.FromDomain(user));
    }

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
