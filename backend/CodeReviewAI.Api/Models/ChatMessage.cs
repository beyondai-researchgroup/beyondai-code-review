namespace CodeReviewAI.Api.Models;

/// <summary>
/// A single turn in a review conversation.
/// </summary>
/// <param name="Role">Sender identity: <c>user</c> or <c>assistant</c>.</param>
/// <param name="Content">Full text of the message.</param>
/// <param name="Timestamp">UTC timestamp when the message was created.</param>
public record ChatMessage(
    string Role,
    string Content,
    DateTime Timestamp);
