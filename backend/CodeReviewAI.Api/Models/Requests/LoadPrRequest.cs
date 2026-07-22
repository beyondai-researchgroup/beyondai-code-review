using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Models.Requests;

/// <summary>
/// Request body for loading a GitHub Pull Request and creating a new review session.
/// </summary>
/// <param name="GitHubToken">
/// Personal Access Token used to authenticate with the GitHub API for this request.
/// Read-only scope is sufficient.
/// </param>
/// <param name="Owner">GitHub repository owner (user or organisation login).</param>
/// <param name="Repo">Repository name, without the owner prefix.</param>
/// <param name="PrNumber">Numeric identifier of the Pull Request.</param>
/// <param name="ReviewMode">
/// How the AI should respond: <see cref="ReviewMode.Ai"/> (interactive chat, default)
/// or <see cref="ReviewMode.Report"/> (single-shot written report).
/// </param>
/// <param name="Lang">UI language code (<c>sr</c> or <c>en</c>). Defaults to <c>sr</c>.</param>
public record LoadPrRequest(
    string GitHubToken,
    string Owner,
    string Repo,
    int PrNumber,
    ReviewMode ReviewMode = ReviewMode.Ai,
    string Lang = "sr");
