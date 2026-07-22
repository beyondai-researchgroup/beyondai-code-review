namespace CodeReviewAI.Api.Models;

/// <summary>
/// An active code-review session for a single Pull Request.
/// Tracks conversation history and activity timestamps.
/// </summary>
public class ReviewSession
{
    /// <summary>Unique session identifier (GUID as string).</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Synchronisation root for mutations of mutable session state (<see cref="History"/> in
    /// particular). The session store's <c>ConcurrentDictionary</c> only protects the map itself,
    /// not the session object — concurrent requests for the same session must lock on this.
    /// </summary>
    public object Sync { get; } = new();

    /// <summary>
    /// GitHub Pull Request data for this session.
    /// Null until <c>POST /api/session/{id}/load-pr</c> succeeds.
    /// </summary>
    public PrContext? PrContext { get; set; }

    /// <summary>
    /// Formatted repository context string (file tree + key file contents).
    /// Null until <c>POST /api/session/{id}/load-repo-context</c> succeeds.
    /// </summary>
    public string? RepoContext { get; set; }

    /// <summary>GitHub PAT stored in-memory for the lifetime of the session (never persisted).</summary>
    public string? GitHubToken { get; set; }

    /// <summary>Repository owner login, stored when the PR is loaded.</summary>
    public string? Owner { get; set; }

    /// <summary>Repository name, stored when the PR is loaded.</summary>
    public string? Repo { get; set; }

    /// <summary>Ordered conversation history between the user and the AI assistant.</summary>
    public List<ChatMessage> History { get; init; } = [];

    /// <summary>UTC timestamp when this session was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent user or assistant message.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The mode selected when the PR was loaded — determines whether the right panel
    /// shows an interactive chat (<see cref="ReviewMode.Ai"/>) or a generated report
    /// (<see cref="ReviewMode.Report"/>).
    /// </summary>
    public ReviewMode Mode { get; set; } = ReviewMode.Ai;

    /// <summary>
    /// Cached generated report for <see cref="ReviewMode.Report"/> sessions.
    /// Null until the first <c>POST /api/session/{id}/report/generate</c> call succeeds.
    /// </summary>
    public string? GeneratedReport { get; set; }

    /// <summary>
    /// AI-generated 2-3 sentence plain-text paraphrase of the PR description.
    /// Null when the PR has no description. Generated once during load-pr and cached here.
    /// </summary>
    public string? ShortSummary { get; set; }

    /// <summary>
    /// The human reviewer's own decision (Accept / Reject) and comment.
    /// Null until the reviewer submits via <c>POST /api/session/{id}/decision</c>.
    /// The AI never sets this property — it is always driven by the human.
    /// </summary>
    public ReviewDecision? Decision { get; set; }

    /// <summary>
    /// Study-flow participant id, set only when the session was created via
    /// <c>POST /api/study/start-review</c>. Null for the (now unused) classic
    /// repo/PR/token loader flow — chat messages and the decision are persisted
    /// to the shared study database only when this is set.
    /// </summary>
    public string? ParticipantId { get; set; }

    /// <summary>
    /// Study-flow session id (1=Intro, 2=AI, 3=Report) from the shared "Sessions"
    /// table, set alongside <see cref="ParticipantId"/>.
    /// </summary>
    public int? StudySessionId { get; set; }
}
