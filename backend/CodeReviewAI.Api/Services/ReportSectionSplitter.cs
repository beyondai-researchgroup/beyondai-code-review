using System.Text.RegularExpressions;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Splits Report/Hybrid mode's `##`-heading documentation markdown into positionally-numbered
/// sections, mirroring <c>ReportViewComponent.splitIntoSections</c> on the frontend exactly (same
/// regex, same 1-based `section-N` numbering) so the ids used here always match the ids the
/// documentation panel itself renders.
/// </summary>
internal static class ReportSectionSplitter
{
    private static readonly Regex HeadingRegex = new(@"^##[ \t]+.+$", RegexOptions.Multiline);

    /// <summary>Returns each `##` section as (positional id, title, body markdown).</summary>
    internal static List<(string Id, string Title, string Body)> Split(string markdown)
    {
        var matches = HeadingRegex.Matches(markdown);
        var result = new List<(string, string, string)>();

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var title = Regex.Replace(match.Value, @"^##[ \t]+", "").Trim();
            var start = match.Index + match.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;
            result.Add(($"section-{i + 1}", title, markdown[start..end].Trim()));
        }

        return result;
    }
}
