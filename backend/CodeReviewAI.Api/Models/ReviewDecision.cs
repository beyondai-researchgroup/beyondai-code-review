using System.Text.Json.Serialization;

namespace CodeReviewAI.Api.Models;

/// <summary>The human reviewer's own decision about a Pull Request.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewDecisionType
{
    Accepted,
    Rejected,
    /// <summary>
    /// Per-app participant timer (2026-09-11) — recorded when the research's Code Review timer
    /// (<c>Research.TimerCodeReviewEnabled/Minutes</c>) expired and the frontend auto-submitted
    /// on the reviewer's behalf. Deliberately a distinct outcome, never coerced to Accepted or
    /// Rejected — the reviewer never actually made that choice, and fabricating one would corrupt
    /// the study's decision data. The <c>Decision</c> column has no DB CHECK constraint, so this
    /// needs no schema change.
    /// </summary>
    TimedOut
}

/// <summary>
/// Records the human reviewer's decision and their written comment.
/// Created when the reviewer clicks Accept or Reject in the Finish modal.
/// </summary>
public record ReviewDecision
{
    public ReviewDecisionType Decision { get; init; }
    public string Comment { get; init; } = string.Empty;
    public DateTime DecidedAt { get; init; }
}
