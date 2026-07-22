namespace CodeReviewAI.Api.Models;

/// <summary>
/// Represents a single file changed within a Pull Request.
/// </summary>
/// <param name="FileName">Repository-relative path of the file.</param>
/// <param name="Status">Change type: <c>added</c>, <c>modified</c>, <c>removed</c>, or <c>renamed</c>.</param>
/// <param name="Additions">Number of lines added in this file.</param>
/// <param name="Deletions">Number of lines removed from this file.</param>
/// <param name="Patch">
/// Unified diff text for this file, or <c>null</c> when the file is binary
/// or the diff is too large to include.
/// </param>
/// <param name="FullContent">
/// Full file content fetched from the head branch when <paramref name="Patch"/> is unavailable.
/// <c>null</c> when neither the diff nor the raw content could be retrieved.
/// </param>
public record PrFile(
    string FileName,
    string Status,
    int Additions,
    int Deletions,
    string? Patch,
    string? FullContent = null);
