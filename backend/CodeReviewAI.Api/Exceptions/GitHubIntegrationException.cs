namespace CodeReviewAI.Api.Exceptions;

/// <summary>
/// Thrown when a GitHub API call fails for a reason surfaceable to the end user,
/// such as a missing pull request, an invalid token, or a rate-limit breach.
/// </summary>
public sealed class GitHubIntegrationException : Exception
{
    /// <summary>Initialises the exception with a user-friendly message.</summary>
    /// <param name="message">Explanation suitable for display in the UI.</param>
    public GitHubIntegrationException(string message) : base(message) { }

    /// <summary>Initialises the exception with a user-friendly message and the underlying Octokit cause.</summary>
    /// <param name="message">Explanation suitable for display in the UI.</param>
    /// <param name="innerException">The original Octokit exception.</param>
    public GitHubIntegrationException(string message, Exception innerException)
        : base(message, innerException) { }
}
