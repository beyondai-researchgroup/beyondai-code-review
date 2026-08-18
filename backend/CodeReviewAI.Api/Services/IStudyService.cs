using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Result of a study login attempt for a participant.
/// </summary>
/// <param name="ParticipantExists">Whether the participant id was found in the shared database.</param>
/// <param name="SessionId">Id of the next unfinished session (1=Intro, 2=AI, 3=Report), if any.</param>
/// <param name="SessionName">Name of the next unfinished session (Intro / AI / Report), if any.</param>
/// <param name="AllFinished">
/// True when the participant exists but has no unfinished sessions left
/// (all done, or none assigned) — access is denied in that case.
/// </param>
/// <param name="ConsentRequired">
/// True when the participant exists but hasn't completed the Consent app's informed-consent
/// step yet (<c>Participant.ConsentGivenAt IS NULL</c>) — the study flow is blocked until they
/// do, same as <see cref="AllFinished"/> blocks it once everything is done. Test participants
/// (<c>Study:TestParticipantIds</c>) bypass this check entirely, same as they bypass the normal
/// session-progression rules.
/// </param>
/// <param name="Language">
/// The language locked at the Consent app (<c>Participant.Language</c>), or <c>null</c> if not
/// yet set. Propagates the participant's one-time language choice into this app instead of
/// letting it pick its own.
/// </param>
public record StudyLoginState(
    bool ParticipantExists,
    int? SessionId,
    string? SessionName,
    bool AllFinished,
    bool ConsentRequired,
    string? Language);

/// <summary>
/// Resolved demo-PR configuration for a participant's research (from the
/// <c>Research</c> table's <c>GitHubOwner</c>/<c>GitHubRepo</c>/<c>GitHubPrNumber</c>/
/// <c>GitHubToken</c> columns).
/// </summary>
public record PrConfig(string Owner, string Repo, int PrNumber, string Token);

/// <summary>
/// Access to the shared study database (Neon Postgres) used by both BeyondAI
/// and the NASA-TLX application.
/// </summary>
public interface IStudyService
{
    /// <summary>
    /// Validates the participant and determines their next unfinished session,
    /// ordered Intro → AI → Report by default, or by each session's
    /// <c>ParticipantSession.SequenceOrder</c> when set (lets an Admin Dashboard
    /// import swap the AI/Report order per participant for counterbalancing).
    /// </summary>
    Task<StudyLoginState> GetLoginStateAsync(string participantId, CancellationToken ct);

    /// <summary>
    /// Resolves the demo PR configuration for a participant's specific session. Prefers an
    /// explicit per-session override (<c>ParticipantSession.PrConfigId</c>, set via the Admin
    /// Dashboard's Excel import — lets different participants, or different sessions of the same
    /// participant, review different PRs concurrently); falls back to the research's single
    /// <c>IsActive</c> config when no override is set. Returns <c>null</c> if the participant has
    /// no research, or nothing resolves — callers should fall back to the legacy global
    /// <c>Study:Pr:*</c>/<c>GitHub:PersonalAccessToken</c> config in that case, which keeps
    /// pre-existing test participants (backfilled to the seeded Test Research with no PR
    /// columns set) working without any manual data migration.
    /// </summary>
    Task<PrConfig?> GetPrConfigForParticipantAsync(string participantId, int sessionId, CancellationToken ct);

    /// <summary>
    /// Persists the reviewer's decision (Accept/Reject + comment) for a study session.
    /// Best-effort: failures are logged internally and never thrown, so a database hiccup
    /// never blocks the participant's flow. Re-submitting the same participant+session
    /// overwrites the previous row (one decision per session, like <c>TlxResult</c>).
    /// </summary>
    Task SaveDecisionAsync(
        string participantId,
        int sessionId,
        ReviewMode reviewMode,
        ReviewDecisionType decision,
        string comment,
        CancellationToken ct);

    /// <summary>
    /// Appends one chat turn (user question or assistant reply) to the study chat log.
    /// Best-effort: failures are logged internally and never thrown.
    /// </summary>
    Task SaveChatMessageAsync(
        string participantId,
        int sessionId,
        string role,
        string content,
        CancellationToken ct);
}
