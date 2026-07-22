namespace CodeReviewAI.Api.Services;

/// <summary>
/// Communicates with the Anthropic Claude API, streaming responses token by token via SSE.
/// </summary>
public interface IClaudeService
{
    /// <summary>
    /// Streams a Claude completion for the given message list.
    /// Yields each text delta as it arrives from the SSE stream.
    /// </summary>
    /// <param name="messages">Ordered conversation messages built by <see cref="IContextManagerService"/>.</param>
    /// <param name="ct">Cancellation token — cancelling mid-stream closes the HTTP connection.</param>
    /// <param name="systemPrompt">
    /// Optional system prompt override. When <c>null</c> the default chat system prompt is used.
    /// Pass <see cref="ClaudeService.ReportSystemPrompt"/> for Report Mode.
    /// </param>
    IAsyncEnumerable<string> StreamResponseAsync(List<ApiMessage> messages, CancellationToken ct, string? systemPrompt = null);
}
