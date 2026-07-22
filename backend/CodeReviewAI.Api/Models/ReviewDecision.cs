using System.Text.Json.Serialization;

namespace CodeReviewAI.Api.Models;

/// <summary>The human reviewer's own decision about a Pull Request.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewDecisionType
{
    Accepted,
    Rejected
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
