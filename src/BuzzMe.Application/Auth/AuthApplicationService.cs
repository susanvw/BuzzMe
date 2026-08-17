using System.Security.Cryptography;
using System.Text;
using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Auth.Models;
using BuzzMe.Application.Users.Models;
using BuzzMe.Domain.Auth;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Auth;

/// <summary>
/// One Application Service for the Auth bounded-context area — APPLICATION_LAYER_SPEC.md
/// §3.10's Register/VerifyAccount/Login/RefreshToken/ForgotPassword/ResetPassword/
/// DeleteAccount. Sprint 9 builds the real, two-phase credential lifecycle IMPLEMENTATION_SPEC.md
/// §2 specifies, superseding Sprint 8's UserApplicationService.ProvisionAccountAsync shortcut
/// (removed). DeleteAccount (Sprint 12) was the last of these to land — it needed Board
/// ownership reassignment (Sprint 10) as a real prerequisite, not an optional nicety.
/// </summary>
public sealed class AuthApplicationService(
    IUserRepository userRepository,
    IBoardRepository boardRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    ISecureTokenGenerator secureTokenGenerator,
    IVerificationCodeGenerator verificationCodeGenerator,
    IIdGenerator idGenerator,
    IClock clock,
    IEmailSender emailSender,
    ISmsSender smsSender)
{
    /// <summary>
    /// No document anywhere fixes these three lifetimes — open operational parameters, same
    /// category as InvitationApplicationService.TokenLifetime. 15 minutes for a
    /// human-typed-from-SMS/email code and 1 hour for a reset link are conventional
    /// defaults; 30 days matches JwtOptions.RefreshTokenLifetimeDays' own existing default
    /// (already present in Api's configuration since the original scaffold, unused until now).
    /// </summary>
    private static readonly TimeSpan VerificationCodeLifetime = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's RegisterAccount. Always mints a new server-generated Id
    /// (IIdGenerator) — unlike Sprint 8's Provision, there is no authenticated caller yet to
    /// source one from. Duplicate email/phone is rejected outright regardless of the
    /// existing record's own Status (a PendingVerification duplicate is still a duplicate —
    /// IMPLEMENTATION_SPEC.md §2's own wording: "rejected, not a second account").
    /// </summary>
    public async Task<Result<RegisterResult>> RegisterAsync(
        string? email, string? phone, string password, string displayName, CancellationToken cancellationToken)
    {
        if (await userRepository.ExistsWithEmailOrPhoneAsync(email, phone, excludingUserId: null, cancellationToken))
            return Result.Failure<RegisterResult>(Error.Conflict("Email or phone is already registered."));

        var now = clock.UtcNow;
        var passwordHash = passwordHasher.Hash(password);
        var code = verificationCodeGenerator.NewCode();

        var user = User.Register(
            new UserId(idGenerator.NewId()), email, phone, passwordHash, new DisplayName(displayName),
            code, now + VerificationCodeLifetime, now);

        await userRepository.AddAsync(user, cancellationToken);

        await SendVerificationCodeAsync(email, phone, code, cancellationToken);

        return Result.Success(new RegisterResult(user.Id.Value));
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's VerifyAccount, immediately followed by the Account
    /// Provisioning policy (APPLICATION_LAYER_SPEC.md §7) in the same request — this
    /// codebase has no saga/outbox-driven retry mechanism wired up anywhere yet (confirmed:
    /// IOutboxWriter is registered but never called by any repository), so "multi-step
    /// orchestrated workflow, retried to completion" is implemented the same way Sprint 5's
    /// AcceptInvitation two-step workflow was — sequential repository calls within one
    /// Application method, not a literal saga. Only Personal Board creation runs; Privacy
    /// Settings/Notification Preferences initialization stay unbuilt (SPRINT_7_REPORT.md
    /// §3.2, SPRINT_8_REPORT.md §3.3 — neither is a buildable aggregate in this codebase).
    /// </summary>
    public async Task<Result<AuthResult>> VerifyAccountAsync(
        string? email, string? phone, string code, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailOrPhoneAsync(email, phone, cancellationToken);
        if (user is null)
            return Result.Failure<AuthResult>(Error.Unauthorized("Invalid or expired code."));

        var now = clock.UtcNow;

        // Natural idempotency (APPLICATION_LAYER_SPEC.md §3.10): re-verifying an
        // already-Active account is a no-op, not an error — the code is never re-checked.
        if (user.Status != UserStatus.Active)
        {
            if (!user.HasValidVerificationCode(code, now))
                return Result.Failure<AuthResult>(Error.Unauthorized("Invalid or expired code."));

            user.Verify(now);
            await userRepository.UpdateAsync(user, cancellationToken);
        }

        if (user.PersonalBoardId is null)
        {
            var personalBoardId = new BoardId(idGenerator.NewId());
            var personalBoard = Board.Create(personalBoardId, new BoardName("Personal"), user.Id.Value, now);
            await boardRepository.AddAsync(personalBoard, cancellationToken);

            user.CompleteProvisioning(personalBoardId);
            await userRepository.UpdateAsync(user, cancellationToken);
        }

        var (accessToken, refreshToken) = await IssueTokenPairAsync(user.Id.Value, now, cancellationToken);
        return Result.Success(new AuthResult(accessToken, refreshToken, UserResult.FromDomain(user)));
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's Login — read-only against User, then session issuance.
    /// Credentials are checked before Status, so a wrong password never distinguishes a
    /// Suspended account from a merely-nonexistent one (API_CONTRACT.md §5: "generic — never
    /// confirms which field was wrong"). PendingVerification and Deleted fall through to the
    /// same generic rejection as a wrong password — IMPLEMENTATION_SPEC.md §2 only carves
    /// out distinct behavior for Suspended (403) and Deactivated (succeeds); see
    /// SPRINT_9_REPORT.md for why every other non-Active status defaults to the generic case
    /// rather than a guessed-at distinct one.
    /// </summary>
    public async Task<Result<AuthResult>> LoginAsync(
        string? email, string? phone, string password, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailOrPhoneAsync(email, phone, cancellationToken);
        if (user is null || !passwordHasher.Verify(password, user.PasswordHash))
            return Result.Failure<AuthResult>(Error.Unauthorized("Invalid email/phone or password."));

        if (user.Status == UserStatus.Suspended)
            return Result.Failure<AuthResult>(Error.Forbidden("This account has been suspended."));

        if (user.Status != UserStatus.Active && user.Status != UserStatus.Deactivated)
            return Result.Failure<AuthResult>(Error.Unauthorized("Invalid email/phone or password."));

        var now = clock.UtcNow;
        var (accessToken, refreshToken) = await IssueTokenPairAsync(user.Id.Value, now, cancellationToken);
        return Result.Success(new AuthResult(accessToken, refreshToken, UserResult.FromDomain(user)));
    }

    /// <summary>
    /// API_CONTRACT.md §5's Refresh Token — "session reissuance only," never touches User.
    /// Rotates on every use: the presented token is revoked before its replacement is
    /// issued, so the same bearer value can never be exchanged twice. Does not re-check the
    /// owning User's Status (e.g. a Suspended account can still refresh) — IMPLEMENTATION_SPEC.md
    /// §2 names exactly one error case for this use case ("expired/revoked refresh token"),
    /// and no document states a cross-check against User.Status; see SPRINT_9_REPORT.md.
    /// </summary>
    public async Task<Result<TokenPairResult>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = ComputeTokenHash(refreshToken);
        var stored = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (stored is null || !stored.IsValid(now))
            return Result.Failure<TokenPairResult>(Error.Unauthorized("Refresh token is invalid or expired."));

        stored.Revoke(now);
        await refreshTokenRepository.UpdateAsync(stored, cancellationToken);

        var (accessToken, newRefreshToken) = await IssueTokenPairAsync(stored.UserId, now, cancellationToken);
        return Result.Success(new TokenPairResult(accessToken, newRefreshToken));
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's RequestAccountRecovery — "never fails visibly." Returns
    /// void, not a Result: API_CONTRACT.md §5 states the endpoint's response is always
    /// `200, {}` whether or not the account exists, so there is no failure case for a caller
    /// to branch on. A second call for the same account overwrites (not stacks) any prior
    /// outstanding token, via User.RequestPasswordReset's own unconditional-overwrite behavior.
    /// </summary>
    public async Task ForgotPasswordAsync(string? email, string? phone, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailOrPhoneAsync(email, phone, cancellationToken);
        if (user is null)
            return;

        var now = clock.UtcNow;
        var token = secureTokenGenerator.NewToken();
        user.RequestPasswordReset(ComputeTokenHash(token), now + PasswordResetTokenLifetime, now);
        await userRepository.UpdateAsync(user, cancellationToken);

        await SendPasswordResetTokenAsync(user.Email, user.Phone, token, cancellationToken);
    }

    /// <summary>IMPLEMENTATION_SPEC.md §2's ConfirmAccountRecovery — the token is consumed by User.ResetPassword itself, so a reused token fails here on the second attempt (API_CONTRACT.md §5: "a used token must not work twice").</summary>
    public async Task<Result> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        var tokenHash = ComputeTokenHash(token);
        var user = await userRepository.GetByPasswordResetTokenHashAsync(tokenHash, cancellationToken);
        var now = clock.UtcNow;

        if (user is null || !user.HasValidPasswordResetToken(tokenHash, now))
            return Result.Failure(Error.Unauthorized("Reset token is invalid, expired, or already used."));

        user.ResetPassword(passwordHasher.Hash(newPassword), now);
        await userRepository.UpdateAsync(user, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// IMPLEMENTATION_SPEC.md §2's ConfirmAccountDeletion — APPLICATION_LAYER_SPEC.md §7's
    /// "multi-aggregate orchestrated workflow": for each Board the requester belongs to,
    /// resolve their Membership (reassign-and-leave if sole Owner with other Active Members;
    /// delete the Board outright if sole Owner with none — IMPLEMENTATION_SPEC.md §4's
    /// ReassignOwnership policy's own stated fallback; otherwise just leave via RemoveMember,
    /// since a non-Owner Membership never blocks anything), each persisted independently
    /// before the next Board is touched — same "sequential, any failed step retried before
    /// the next proceeds" shape as every other multi-aggregate workflow in this codebase
    /// (no saga infrastructure exists, see VerifyAccountAsync's own doc comment). Then revokes
    /// every outstanding session and marks the User Deleted. Anonymizing authorship on shared
    /// Boards' History and the async Purge of the Personal Board's actual content are NOT
    /// done here — see SPRINT_12_REPORT.md's gaps (no History/audit entity exists anywhere in
    /// this codebase, and no Purge background worker has ever been built, for any aggregate).
    /// </summary>
    public async Task<Result> DeleteAccountAsync(Guid requestingUserId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(new UserId(requestingUserId), cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("User not found."));

        if (user.Status == UserStatus.Deleted)
            return Result.Success();

        var now = clock.UtcNow;

        // DOMAIN_MODEL.md's family/team scale (same reasoning as ListMembersAsync's
        // in-memory pagination, Sprint 11) — one generous page is always enough in practice
        // for "every Board a single person belongs to."
        var boards = await boardRepository.ListByMemberAsync(requestingUserId, afterId: null, limit: 1000, cancellationToken);

        foreach (var board in boards)
        {
            if (board.OwnerUserId == requestingUserId)
            {
                var hasOtherActiveMembers = board.Memberships.Any(m => m.Status == MembershipStatus.Active && m.UserId != requestingUserId);
                if (hasOtherActiveMembers)
                    board.Leave(requestingUserId, now);
                else
                    board.Delete(now);
            }
            else
            {
                board.RemoveMember(requestingUserId, now, requestingUserId);
            }

            await boardRepository.UpdateAsync(board, cancellationToken);
        }

        await refreshTokenRepository.RevokeAllForUserAsync(requestingUserId, now, cancellationToken);

        user.Delete(now);
        await userRepository.UpdateAsync(user, cancellationToken);

        return Result.Success();
    }

    private async Task<(string AccessToken, string RefreshToken)> IssueTokenPairAsync(
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var accessToken = accessTokenIssuer.Issue(userId, now);

        var refreshTokenValue = secureTokenGenerator.NewToken();
        var refreshToken = RefreshToken.Issue(
            new RefreshTokenId(idGenerator.NewId()), userId, ComputeTokenHash(refreshTokenValue), now + RefreshTokenLifetime, now);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        return (accessToken, refreshTokenValue);
    }

    /// <summary>IMPLEMENTATION_SPEC.md §8: "Send a verification code (email/SMS)" — reuses the IEmailSender/ISmsSender abstractions Sprint 6 built (and documented for exactly this purpose) but never called from anywhere until now.</summary>
    private async Task SendVerificationCodeAsync(string? email, string? phone, string code, CancellationToken cancellationToken)
    {
        if (email is not null)
            await emailSender.SendAsync(email, "Your BuzzMe verification code", $"Your verification code is {code}.", cancellationToken);
        else if (phone is not null)
            await smsSender.SendAsync(phone, $"Your BuzzMe verification code is {code}.", cancellationToken);
    }

    /// <summary>IMPLEMENTATION_SPEC.md §8: "Send a recovery token (email/SMS)."</summary>
    private async Task SendPasswordResetTokenAsync(string? email, string? phone, string token, CancellationToken cancellationToken)
    {
        if (email is not null)
            await emailSender.SendAsync(email, "Reset your BuzzMe password", $"Your password reset token is {token}.", cancellationToken);
        else if (phone is not null)
            await smsSender.SendAsync(phone, $"Your BuzzMe password reset token is {token}.", cancellationToken);
    }

    /// <summary>
    /// A pure, deterministic digest — not a slow/salted hash like passwords, because
    /// RefreshToken/PasswordResetToken values are already 256-bit cryptographically random
    /// (ISecureTokenGenerator), so a fast hash is standard practice for at-rest storage of
    /// an already-high-entropy bearer credential, and is what makes an equality lookup by
    /// hash possible at all.
    /// </summary>
    private static string ComputeTokenHash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
