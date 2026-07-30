using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Represents a single message in an Anthropic API conversation turn.
/// </summary>
/// <param name="Role">Either <c>user</c> or <c>assistant</c>.</param>
/// <param name="Content">Text content of the message.</param>
public record ApiMessage(string Role, string Content);

/// <summary>
/// Builds the ordered list of messages to send to the Claude API for a given
/// user question, incorporating PR context and prior conversation history.
/// </summary>
public interface IContextManagerService
{
    /// <summary>
    /// Constructs the full messages array for a Claude API call.
    /// </summary>
    /// <param name="pr">The Pull Request context for this session.</param>
    /// <param name="history">Prior conversation turns for this session.</param>
    /// <param name="userQuestion">The current question from the user.</param>
    /// <param name="repoContext">Optional pre-fetched repository file-tree context.</param>
    /// <param name="docsContent">
    /// Optional pre-fetched reference material (e.g. a standard/specification) from the
    /// repository's Docs folder. Only included when <paramref name="userQuestion"/> appears
    /// to be asking about a standard — see the implementation's trigger-word check.
    /// </param>
    /// <returns>
    /// An ordered list of <see cref="ApiMessage"/> objects ready to be serialised
    /// and sent to the Anthropic Messages API.
    /// </returns>
    List<ApiMessage> BuildMessages(PrContext pr, List<ChatMessage> history, string userQuestion, string? repoContext = null, string? docsContent = null, string lang = "sr");

    /// <summary>
    /// Constructs the message list for a single-shot PR report.
    /// Includes ALL file patches (no heuristic trimming) and ends with a fixed
    /// generation instruction instead of a free-form user question.
    /// No conversation history is included.
    /// </summary>
    /// <param name="pr">The Pull Request context for this session.</param>
    /// <param name="lang">UI language code (<c>sr</c> or <c>en</c>).</param>
    /// <returns>
    /// An ordered list of <see cref="ApiMessage"/> objects ready to be sent to the
    /// Anthropic Messages API with the Report system prompt.
    /// </returns>
    List<ApiMessage> BuildReportMessages(PrContext pr, string lang = "sr");

    /// <summary>
    /// Constructs the message list that asks Claude to suggest 4 follow-up questions
    /// based on the PR context and the current conversation history.
    /// Questions already asked by the user are passed so Claude can avoid repeating them.
    /// </summary>
    /// <param name="pr">The Pull Request context for this session.</param>
    /// <param name="history">Prior conversation turns for this session.</param>
    /// <param name="lang">UI language code (<c>sr</c> or <c>en</c>).</param>
    /// <returns>
    /// An ordered list of <see cref="ApiMessage"/> objects. The expected Claude response
    /// is a JSON array of exactly 4 question strings.
    /// </returns>
    List<ApiMessage> BuildSuggestionsMessages(PrContext pr, List<ChatMessage> history, string lang = "sr");

    /// <summary>
    /// Constructs a minimal message list that asks Claude to produce a 2-3 sentence
    /// plain-text paraphrase of the provided PR description.
    /// Returns <c>null</c> when <paramref name="description"/> is blank, signalling
    /// that no Claude call is needed.
    /// </summary>
    /// <param name="description">The raw PR body text from GitHub.</param>
    /// <param name="lang">UI language code (<c>sr</c> or <c>en</c>).</param>
    /// <returns>
    /// An ordered list of <see cref="ApiMessage"/> objects, or <c>null</c> if the
    /// description is empty.
    /// </returns>
    List<ApiMessage>? BuildShortSummaryMessages(string? description, string lang = "sr");
}
