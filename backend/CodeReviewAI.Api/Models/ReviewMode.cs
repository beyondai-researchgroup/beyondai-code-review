using System.Text.Json.Serialization;

namespace CodeReviewAI.Api.Models;

/// <summary>
/// Determines how the AI assistant responds to the PR for a given session.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewMode
{
    /// <summary>Interactive chat — user asks questions and the AI responds incrementally via SSE.</summary>
    Ai,

    /// <summary>Single-shot report — the AI generates one comprehensive report, no further interaction.</summary>
    Report
}
