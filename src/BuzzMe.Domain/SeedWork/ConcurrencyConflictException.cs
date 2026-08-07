namespace BuzzMe.Domain.SeedWork;

/// <summary>
/// A version-checked write matched zero documents — someone else modified this aggregate
/// between load and save. Sprint 6 is the first place this codebase actually enforces
/// <see cref="AggregateRoot{TId}.Version"/> as a real optimistic-concurrency gate (every
/// prior repository's write path left it unenforced — see SPRINT_5_REPORT.md §4.3/§5.1).
/// A genuine, unexpected-under-normal-operation fault, same treatment as a duplicate-key
/// violation elsewhere in this codebase — not a Result/Error outcome.
/// </summary>
public sealed class ConcurrencyConflictException(string message) : Exception(message);
