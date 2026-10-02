using System.Text.Json.Serialization;

namespace CodeReviewAI.Api.Models;

/// <summary>
/// Whether a Hybrid-mode documentation accordion section was expanded or collapsed —
/// recorded per event, never for Ai/Report sessions. See <see cref="ReviewMode.Hybrid"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HybridSectionAction
{
    Expand,
    Collapse
}
