namespace CodeReviewAI.Api.Models.Responses;

/// <summary>
/// Lightweight summary of a Pull Request returned after a successful load.
/// Patch text is intentionally excluded to keep the payload small.
/// </summary>
/// <param name="Title">Title of the Pull Request.</param>
/// <param name="Author">GitHub login of the PR author.</param>
/// <param name="BaseBranch">Name of the target branch (e.g. <c>main</c>).</param>
/// <param name="HeadBranch">Name of the source (feature) branch.</param>
/// <param name="Description">PR body text, or <c>null</c> if the author left it blank.</param>
/// <param name="Files">Changed files without patch content.</param>
/// <param name="TotalAdditions">Sum of line additions across all changed files.</param>
/// <param name="TotalDeletions">Sum of line deletions across all changed files.</param>
/// <param name="ShortSummary">2-3 sentence plain-text paraphrase of the description — AI-generated, or a fixed predefined text when <c>Session:UseStaticSummary</c> is on (see <see cref="Services.StaticSummaryContent"/>).</param>
public record PrSummaryResponse(
    string Title,
    string Author,
    string BaseBranch,
    string HeadBranch,
    string? Description,
    List<PrFileSummary> Files,
    int TotalAdditions,
    int TotalDeletions,
    string? ShortSummary = null);

/// <summary>
/// Per-file entry in <see cref="PrSummaryResponse"/>, omitting the diff patch.
/// </summary>
/// <param name="FileName">Repository-relative path of the file.</param>
/// <param name="Status">Change type: <c>added</c>, <c>modified</c>, <c>removed</c>, or <c>renamed</c>.</param>
/// <param name="Additions">Number of lines added.</param>
/// <param name="Deletions">Number of lines removed.</param>
public record PrFileSummary(
    string FileName,
    string Status,
    int Additions,
    int Deletions);
