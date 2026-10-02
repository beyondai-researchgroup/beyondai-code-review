namespace CodeReviewAI.Api.Models.Requests;

/// <summary>
/// Request body for <c>POST /api/session/{sessionId}/activity-log</c>.
/// Carries one participant activity entry — an instantaneous action has
/// <see cref="StartedAt"/> equal to <see cref="EndedAt"/> (or <see cref="EndedAt"/> omitted); a
/// genuine duration (a chat reply streaming, a panel drag) carries both.
/// </summary>
public record ActivityLogRequest
{
    public string EventType { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
}
