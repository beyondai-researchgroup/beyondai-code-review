namespace CodeReviewAI.Api.Models.Requests;

/// <summary>
/// Request body for <c>POST /api/session/{sessionId}/hybrid/section-event</c>.
/// Carries one expand/collapse event for a Hybrid-mode documentation accordion section.
/// </summary>
public record HybridSectionEventRequest
{
    public string SectionId { get; init; } = string.Empty;
    public string SectionTitle { get; init; } = string.Empty;
    public HybridSectionAction Action { get; init; }
    public int? DurationSeconds { get; init; }
}
