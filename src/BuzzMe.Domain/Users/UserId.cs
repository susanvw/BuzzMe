namespace BuzzMe.Domain.Users;

/// <summary>A User's identity — same pattern as BoardId/ReminderId. Deliberately equal to the identity a caller's JWT already establishes (Sprint 8's ProvisionAccountAsync never generates a new one — see Users/UserApplicationService.cs).</summary>
public readonly record struct UserId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
