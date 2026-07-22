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
public record StudyLoginState(
    bool ParticipantExists,
    int? SessionId,
    string? SessionName,
    bool AllFinished);

/// <summary>
/// Access to the shared study database (Neon Postgres) used by both BeyondAI
/// and the NASA-TLX application.
/// </summary>
public interface IStudyService
{
    /// <summary>
    /// Validates the participant and determines their next unfinished session,
    /// ordered Intro → AI → Report.
    /// </summary>
    Task<StudyLoginState> GetLoginStateAsync(string participantId, CancellationToken ct);

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
