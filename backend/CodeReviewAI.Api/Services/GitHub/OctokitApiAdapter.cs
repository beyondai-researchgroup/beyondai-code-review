using System.Text;
using CodeReviewAI.Api.Exceptions;
using Octokit;

namespace CodeReviewAI.Api.Services.GitHub;

/// <summary>
/// Implements <see cref="IGitHubApiAdapter"/> using Octokit.net.
/// Creates a per-request <see cref="GitHubClient"/> authenticated with the caller-supplied token.
/// </summary>
internal sealed class OctokitApiAdapter : IGitHubApiAdapter
{
    private static readonly ProductHeaderValue ProductHeader = new("CodeReviewAI");

    private const int MaxPrFileChars = 8_000;

    private static readonly string[] ConfigFileNames =
    [
        "package.json", "go.mod", "Cargo.toml", "pom.xml",
        "requirements.txt", "setup.py", "pyproject.toml",
        "Dockerfile", "docker-compose.yml", "docker-compose.yaml"
    ];

    private static readonly string[] ReadmeNames =
    [
        "README.md", "README.rst", "README.txt", "README"
    ];

    private static readonly string[] DocsFolders =
    [
        "docs/", "doc/", "documentation/", "wiki/"
    ];

    private const int MaxKeyFiles = 12;
    private const int MaxDocFiles = 5;
    private const int MaxFileChars = 4_000;
    private const int MaxTreePaths = 300;

    /// <inheritdoc />
    public async Task<GitHubPrData> GetPrDataAsync(
        string token, string owner, string repo, int prNumber, CancellationToken ct)
    {
        var client = new GitHubClient(ProductHeader) { Credentials = new Credentials(token) };

        try
        {
            ct.ThrowIfCancellationRequested();
            var pr = await client.PullRequest.Get(owner, repo, prNumber);

            ct.ThrowIfCancellationRequested();
            var files = await client.PullRequest.Files(owner, repo, prNumber);

            ct.ThrowIfCancellationRequested();
            var commits = await client.PullRequest.Commits(owner, repo, prNumber);

            // For files where GitHub did not provide a diff (binary or very large files),
            // or where the diff exceeds the cap that GitHubService will apply anyway,
            // fall back to fetching the full file content from the head branch.
            var headRef = pr.Head.Ref;
            var enrichedFiles = new List<GitHubFileData>(files.Count);
            foreach (var f in files)
            {
                var patchUnusable = f.Patch is null || f.Patch.Length > GitHubService.MaxPatchLength;
                if (patchUnusable && !string.Equals(f.Status, "removed", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        var bytes = await client.Repository.Content.GetRawContentByRef(owner, repo, f.FileName, headRef);
                        var text = Encoding.UTF8.GetString(bytes);
                        if (text.Length > MaxPrFileChars)
                            text = text[..MaxPrFileChars] + "\n... [sadržaj skraćen]";
                        enrichedFiles.Add(new GitHubFileData(f.FileName, f.Status, f.Additions, f.Deletions, f.Patch, text));
                    }
                    catch
                    {
                        enrichedFiles.Add(new GitHubFileData(f.FileName, f.Status, f.Additions, f.Deletions, f.Patch));
                    }
                }
                else
                {
                    enrichedFiles.Add(new GitHubFileData(f.FileName, f.Status, f.Additions, f.Deletions, f.Patch));
                }
            }

            return new GitHubPrData(
                pr.Title,
                pr.Body,
                pr.User.Login,
                pr.Base.Ref,
                pr.Head.Ref,
                enrichedFiles,
                commits.Select(c => c.Commit.Message).ToList());
        }
        catch (NotFoundException ex)
        {
            throw new GitHubIntegrationException(
                $"Pull request #{prNumber} was not found in {owner}/{repo}.", ex);
        }
        catch (AuthorizationException ex)
        {
            throw new GitHubIntegrationException(
                "The provided GitHub token is invalid or lacks the required permissions.", ex);
        }
        catch (RateLimitExceededException ex)
        {
            throw new GitHubIntegrationException(
                "GitHub API rate limit exceeded. Please try again later.", ex);
        }
        catch (ApiException ex)
        {
            throw new GitHubIntegrationException($"GitHub API error: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<GitHubRepoContextData> GetRepoContextDataAsync(
        string token, string owner, string repo, CancellationToken ct)
    {
        var client = new GitHubClient(ProductHeader) { Credentials = new Credentials(token) };

        try
        {
            ct.ThrowIfCancellationRequested();

            // Resolve HEAD tree SHA via the default branch.
            var repoInfo = await client.Repository.Get(owner, repo);
            var defaultBranch = repoInfo.DefaultBranch;

            ct.ThrowIfCancellationRequested();
            var branch = await client.Repository.Branch.Get(owner, repo, defaultBranch);
            var commitSha = branch.Commit.Sha;

            ct.ThrowIfCancellationRequested();
            var commit = await client.Git.Commit.Get(owner, repo, commitSha);
            var treeSha = commit.Tree.Sha;

            ct.ThrowIfCancellationRequested();
            var tree = await client.Git.Tree.GetRecursive(owner, repo, treeSha);

            var allPaths = tree.Tree
                .Where(t => t.Type.Value == TreeType.Blob)
                .Select(t => t.Path)
                .Take(MaxTreePaths)
                .ToList();

            var keyPaths = SelectKeyFiles(allPaths);

            var keyFileContents = new List<(string Path, string Content)>();
            foreach (var path in keyPaths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var bytes = await client.Repository.Content.GetRawContent(owner, repo, path);
                    var text = Encoding.UTF8.GetString(bytes);
                    if (text.Length > MaxFileChars)
                        text = text[..MaxFileChars] + "\n... [sadržaj skraćen]";
                    keyFileContents.Add((path, text));
                }
                catch
                {
                    // Skip files that can't be read (binary, permissions, etc.).
                }
            }

            return new GitHubRepoContextData(owner, repo, allPaths, keyFileContents);
        }
        catch (NotFoundException ex)
        {
            throw new GitHubIntegrationException(
                $"Repository {owner}/{repo} was not found.", ex);
        }
        catch (AuthorizationException ex)
        {
            throw new GitHubIntegrationException(
                "The provided GitHub token is invalid or lacks the required permissions.", ex);
        }
        catch (RateLimitExceededException ex)
        {
            throw new GitHubIntegrationException(
                "GitHub API rate limit exceeded. Please try again later.", ex);
        }
        catch (ApiException ex)
        {
            throw new GitHubIntegrationException($"GitHub API error: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GitHubIntegrationException(
                $"Unexpected error while fetching repository context for {owner}/{repo}: {ex.Message}", ex);
        }
    }

    private static IEnumerable<string> SelectKeyFiles(IReadOnlyList<string> paths)
    {
        var selected = new List<string>();

        // 1. README at repo root (highest priority).
        var readme = paths.FirstOrDefault(p =>
            ReadmeNames.Any(n => string.Equals(p, n, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(Path.GetFileName(p), n, StringComparison.OrdinalIgnoreCase) &&
                                 !p.Contains('/')));
        if (readme is null)
            readme = paths.FirstOrDefault(p =>
                ReadmeNames.Any(n => string.Equals(Path.GetFileName(p), n, StringComparison.OrdinalIgnoreCase)));
        if (readme is not null) selected.Add(readme);

        // 2. Other markdown files at repo root (e.g. CLAUDE.md, CONTRIBUTING.md, CHANGELOG.md).
        var rootMdFiles = paths
            .Where(p => !p.Contains('/') && p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                        && !selected.Contains(p))
            .OrderBy(p => p);
        foreach (var md in rootMdFiles)
            if (!selected.Contains(md)) selected.Add(md);

        // 3. Markdown files from docs/doc/documentation/wiki folders (project documentation).
        var docFiles = paths
            .Where(p => DocsFolders.Any(d => p.StartsWith(d, StringComparison.OrdinalIgnoreCase))
                        && p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p)
            .Take(MaxDocFiles);
        foreach (var doc in docFiles)
            if (!selected.Contains(doc)) selected.Add(doc);

        // 4. Known config/manifest files.
        foreach (var name in ConfigFileNames)
        {
            var match = paths.FirstOrDefault(p =>
                string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !selected.Contains(match))
                selected.Add(match);
        }

        // 5. C# project files.
        var csproj = paths.FirstOrDefault(p =>
            p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csproj is not null && !selected.Contains(csproj))
            selected.Add(csproj);

        return selected.Take(MaxKeyFiles);
    }
}
