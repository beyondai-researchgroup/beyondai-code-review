namespace CodeReviewAI.Api.Services.GitHub;

/// <summary>Raw PR data returned by the GitHub API, prior to any domain mapping.</summary>
internal record GitHubPrData(
    string Title,
    string? Body,
    string AuthorLogin,
    string BaseRef,
    string HeadRef,
    IReadOnlyList<GitHubFileData> Files,
    IReadOnlyList<string> CommitMessages);

/// <summary>Raw file-change entry returned by the GitHub API.</summary>
internal record GitHubFileData(
    string Filename,
    string Status,
    int Additions,
    int Deletions,
    string? Patch,
    string? FullContent = null);

/// <summary>Repository context data: full file tree + content of key files.</summary>
internal record GitHubRepoContextData(
    string Owner,
    string Repo,
    IReadOnlyList<string> AllPaths,
    IReadOnlyList<(string Path, string Content)> KeyFileContents);

/// <summary>
/// Thin wrapper over the GitHub API client. Converts Octokit exceptions into
/// <see cref="Exceptions.GitHubIntegrationException"/> before they reach the service layer.
/// </summary>
internal interface IGitHubApiAdapter
{
    /// <summary>
    /// Fetches metadata, file changes, and commit messages for a single Pull Request.
    /// </summary>
    Task<GitHubPrData> GetPrDataAsync(string token, string owner, string repo, int prNumber, CancellationToken ct);

    /// <summary>
    /// Fetches the repository file tree and content of key configuration/documentation files.
    /// </summary>
    Task<GitHubRepoContextData> GetRepoContextDataAsync(string token, string owner, string repo, CancellationToken ct);
}
