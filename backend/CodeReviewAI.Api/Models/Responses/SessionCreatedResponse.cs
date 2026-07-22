namespace CodeReviewAI.Api.Models.Responses;

/// <summary>
/// Returned after a review session is successfully created.
/// </summary>
/// <param name="SessionId">
/// Unique identifier for the new session. Pass this value in subsequent
/// <c>ChatRequest</c> calls to continue the conversation.
/// </param>
public record SessionCreatedResponse(string SessionId);
