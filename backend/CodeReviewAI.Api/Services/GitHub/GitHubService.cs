using System.Text;
using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services.GitHub;

/// <summary>
/// GitHub implementation of <see cref="IRemoteRepositoryService"/>.
/// Applies domain rules (patch size cap) to the raw data returned by
/// <see cref="IGitHubApiAdapter"/> and maps it into a <see cref="PrContext"/>.
/// </summary>
internal sealed class GitHubService : IRemoteRepositoryService
{
    /// <summary>
    /// Patches longer than this character count are omitted to keep AI prompts
    /// within token limits and avoid overloading the UI. The adapter uses the same
    /// threshold to decide when to fetch full file content as a fallback.
    /// </summary>
    internal const int MaxPatchLength = 15_000;

    private readonly IGitHubApiAdapter _adapter;

    /// <summary>Creates a new <see cref="GitHubService"/>.</summary>
    public GitHubService(IGitHubApiAdapter adapter) => _adapter = adapter;

    /// <inheritdoc />
    public async Task<PrContext> LoadPrContextAsync(
        string token, string owner, string repo, int prNumber, CancellationToken ct)
    {
        var data = await _adapter.GetPrDataAsync(token, owner, repo, prNumber, ct);

        var files = data.Files
            .Select(f =>
            {
                var patch = f.Patch is not null && f.Patch.Length <= MaxPatchLength ? f.Patch : null;
                return new PrFile(f.Filename, f.Status, f.Additions, f.Deletions, patch, f.FullContent);
            })
            .ToList();

        return new PrContext(
            data.Title,
            data.Body,
            data.AuthorLogin,
            data.BaseRef,
            data.HeadRef,
            files,
            [.. data.CommitMessages],
            DateTime.UtcNow);
    }

    /// <inheritdoc />
    public async Task<string> LoadRepoContextAsync(
        string token, string owner, string repo, CancellationToken ct)
    {
        var data = await _adapter.GetRepoContextDataAsync(token, owner, repo, ct);
        return BuildRepoContextString(data);
    }

    private static string BuildRepoContextString(GitHubRepoContextData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Kontekst repozitorijuma: {data.Owner}/{data.Repo}");
        sb.AppendLine();

        if (data.AllPaths.Count > 0)
        {
            sb.AppendLine("### Struktura projekta");
            foreach (var path in data.AllPaths)
                sb.AppendLine($"- {path}");
            sb.AppendLine();
        }

        foreach (var (path, content) in data.KeyFileContents)
        {
            var ext = Path.GetExtension(path).TrimStart('.');
            var lang = ext switch
            {
                "json" => "json",
                "toml" => "toml",
                "xml"  => "xml",
                "py"   => "python",
                "go"   => "go",
                _      => ""
            };
            sb.AppendLine($"### {path}");
            sb.AppendLine($"```{lang}");
            sb.AppendLine(content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
