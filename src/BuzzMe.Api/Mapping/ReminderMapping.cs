using BuzzMe.Application.Common;
using BuzzMe.Application.Reminders.Models;
using BuzzMe.Contracts.V1.Common;
using BuzzMe.Contracts.V1.Reminders;

namespace BuzzMe.Api.Mapping;

/// <summary>Application → Contracts mapping for Reminders — extension methods, not a generic mapper (DEVELOPMENT_GUIDE.md §3).</summary>
public static class ReminderMapping
{
    public static ReminderResponse ToResponse(this ReminderResult result) => new(
        result.Id,
        result.BoardId,
        result.Title,
        result.Recurrence,
        result.StartDate,
        result.ReferenceTimezone,
        result.NotifyPreset,
        NextOccurrence: null,
        result.CreatedAt,
        result.UpdatedAt);

    public static ApiListResponse<ReminderResponse> ToListResponse(this PagedResult<ReminderResult> paged) =>
        ApiListResponse<ReminderResponse>.Ok(
            paged.Items.Select(item => item.ToResponse()).ToList(),
            new PaginationInfo(paged.NextCursor));
}
