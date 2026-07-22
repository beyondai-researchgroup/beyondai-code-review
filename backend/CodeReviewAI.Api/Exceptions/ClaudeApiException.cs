namespace CodeReviewAI.Api.Exceptions;

/// <summary>
/// Thrown when the Anthropic Claude API returns a non-2xx response.
/// </summary>
public sealed class ClaudeApiException : Exception
{
    /// <summary>HTTP status code returned by the API.</summary>
    public int StatusCode { get; }

    /// <summary>Initialises the exception with the HTTP status and raw response body.</summary>
    /// <param name="statusCode">HTTP status code (e.g. 401, 429, 500).</param>
    /// <param name="responseBody">Raw error body returned by the API.</param>
    public ClaudeApiException(int statusCode, string responseBody)
        : base($"Claude API returned HTTP {statusCode}: {responseBody}")
    {
        StatusCode = statusCode;
    }
}
