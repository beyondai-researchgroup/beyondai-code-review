using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Fetches Pull Request data from a remote source control platform.
/// </summary>
public interface IRemoteRepositoryService
{
    /// <summary>
    /// Loads all data required to review a Pull Request and returns it as a
    /// <see cref="PrContext"/> ready for prompt construction.
    /// </summary>
    /// <param name="token">
    /// Personal Access Token used to authenticate with the platform API.
    /// Supplied per-request — never stored or injected via DI.
    /// </param>
    /// <param name="owner">Repository owner login (user or organisation).</param>
    /// <param name="repo">Repository name without the owner prefix.</param>
    /// <param name="prNumber">Numeric identifier of the Pull Request.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>A fully-populated <see cref="PrContext"/>.</returns>
    /// <exception cref="Exceptions.GitHubIntegrationException">
    /// Thrown when the PR does not exist, the token is invalid, or the API rate limit is exceeded.
    /// </exception>
    Task<PrContext> LoadPrContextAsync(string token, string owner, string repo, int prNumber, CancellationToken ct);

    /// <summary>
    /// Builds a formatted context string for the entire repository, including the file tree
    /// and content of key configuration and documentation files.
    /// </summary>
    /// <param name="token">Personal Access Token for this request.</param>
    /// <param name="owner">Repository owner login.</param>
    /// <param name="repo">Repository name without the owner prefix.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A markdown-formatted string ready to be injected into the AI prompt.</returns>
    Task<string> LoadRepoContextAsync(string token, string owner, string repo, CancellationToken ct);
}
