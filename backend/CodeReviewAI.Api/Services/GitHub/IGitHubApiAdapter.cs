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

    /// <summary>
    /// Fetches and extracts text from every file under a repository's <c>Docs/</c> folder
    /// (on the default branch), for use as background reference material the AI can draw
    /// on without the file ever being exposed in the PR file list or diff viewer. PDF files
    /// have their text extracted; other files are read as plain UTF-8 text. Returns
    /// <c>null</c> when the folder doesn't exist or contains nothing readable — this is a
    /// best-effort extra, never a hard requirement for the session to load.
    /// </summary>
    Task<string?> GetDocsContentAsync(string token, string owner, string repo, CancellationToken ct);
}
