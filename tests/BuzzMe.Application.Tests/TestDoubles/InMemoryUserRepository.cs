using BuzzMe.Domain.Users;

namespace BuzzMe.Application.Tests.TestDoubles;

/// <summary>
/// An in-memory IUserRepository — same rationale as InMemoryBoardRepository: Application
/// Service orchestration tested against a fake, the real one exercised only by
/// Infrastructure integration tests against real MongoDB.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

    public Task<bool> ExistsWithEmailOrPhoneAsync(
        string? email, string? phone, UserId? excludingUserId, CancellationToken cancellationToken)
    {
        var matches = _users.Where(user => excludingUserId is null || user.Id != excludingUserId.Value)
            .Any(user =>
                (!string.IsNullOrWhiteSpace(email) && user.Email == email) ||
                (!string.IsNullOrWhiteSpace(phone) && user.Phone == phone));

        return Task.FromResult(matches);
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken) =>
        // No-op: the fake holds User by reference — the caller already mutated it via
        // user.UpdateProfile(...) before calling this. Same reasoning as
        // InMemoryBoardRepository.AddMemberAsync.
        Task.CompletedTask;
}
