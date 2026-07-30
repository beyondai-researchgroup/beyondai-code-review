namespace CodeReviewAI.Api.Models.Requests;

/// <summary>
/// Request body for <c>POST /api/session/{sessionId}/mode</c>.
/// </summary>
public record SwitchModeRequest
{
    public ReviewMode Mode { get; init; }
}
