namespace CodeReviewAI.Api.Models.Requests;

/// <summary>
/// Request body for <c>POST /api/session/{sessionId}/decision</c>.
/// Carries the human reviewer's own decision and their written comment.
/// </summary>
public record SubmitDecisionRequest
{
    public ReviewDecisionType Decision { get; init; }
    public string Comment { get; init; } = string.Empty;
}
