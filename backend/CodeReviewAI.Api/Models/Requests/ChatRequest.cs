namespace CodeReviewAI.Api.Models.Requests;

/// <summary>
/// Request body for sending a follow-up message within an existing review session.
/// The session is identified by the URL path, not the body.
/// </summary>
/// <param name="Message">The user's question or comment about the Pull Request.</param>
/// <param name="Lang">UI language code (<c>sr</c> or <c>en</c>). Defaults to <c>sr</c>.</param>
public record ChatRequest(
    string Message,
    string Lang = "sr");
