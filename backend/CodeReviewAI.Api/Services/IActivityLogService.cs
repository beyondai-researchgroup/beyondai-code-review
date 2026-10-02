using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Builds a per-review-session activity-log CSV — every meaningful thing a participant does
/// while a session is active (clicks, chat messages, documentation search, section engagement,
/// panel resizes, decision submission, ...), with start/end timestamps — entirely in memory (one
/// <see cref="System.Text.StringBuilder"/> per session, keyed by the opaque id <see cref="CreateLog"/>
/// returns). Rewritten 2026-10-01 from an earlier local-disk-file implementation: this is a real
/// research instrument and must keep working once the app is hosted (a Render container's local
/// disk is ephemeral and not shared across instances), and Postgres (via
/// <see cref="StudyService.SaveActivityLogAsync"/>) already persists the finished CSV correctly in
/// production regardless — the local file was never actually needed for that, only for this
/// in-process accumulation step. Always enabled now (no configuration key gates it anymore).
/// </summary>
public interface IActivityLogService
{
    /// <summary>
    /// Starts a new in-memory log for this session (writes the header row + an initial
    /// "SessionStarted" row) and returns its id — an opaque string, not a file path, despite the
    /// property it's commonly stored on (<c>ReviewSession.ActivityLogFilePath</c>) still being
    /// named that for historical reasons.
    /// </summary>
    string CreateLog(string sessionId, string participantId, int studySessionId, ReviewMode mode);

    /// <summary>
    /// Appends one activity row to the given log. No-ops silently when <paramref name="logId"/>
    /// is <c>null</c> (should not normally happen now that logging is always enabled, but kept
    /// for defensive parity with every other best-effort write in this codebase).
    /// </summary>
    void LogEvent(
        string? logId,
        string participantId,
        int studySessionId,
        ReviewMode mode,
        string eventType,
        string? detail,
        DateTime startedAt,
        DateTime? endedAt);

    /// <summary>Returns the log's full accumulated CSV text, or <c>null</c> if the id is unknown.</summary>
    string? GetContent(string? logId);

    /// <summary>
    /// Frees the in-memory buffer once a session is truly finished (its activity log has been
    /// persisted for the last time) — called from the session's actual teardown paths
    /// (<c>DeleteSession</c>, <see cref="SessionCleanupService"/>'s eviction sweep), never from the
    /// mid-session "decision submitted" persist, so the buffer survives for whatever still appends
    /// to it afterward (e.g. the final "SessionEnded" row).
    /// </summary>
    void ReleaseLog(string? logId);
}
