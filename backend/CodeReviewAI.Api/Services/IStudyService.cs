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
/// True when the participant's research requires consent (<c>Research.UsesConsentForm</c>) and
/// they haven't completed the Consent app's informed-consent step yet
/// (<c>Participant.ConsentGivenAt IS NULL</c>) — the study flow is blocked until they do, same as
/// <see cref="AllFinished"/> blocks it once everything is done. Test participants
/// (<c>Participant.IsTestParticipant</c>) bypass this check entirely, same as they bypass the
/// normal session-progression rules.
/// </param>
/// <param name="Language">
/// The language locked at the Consent app (<c>Participant.Language</c>), or <c>null</c> if not
/// yet set. Propagates the participant's one-time language choice into this app instead of
/// letting it pick its own.
/// </param>
/// <param name="AmbiguousParticipantId">
/// True when more than one <c>Participant</c> row shares this bare id across different
/// researches (item 1 of the "platform improvements round 2" plan — ParticipantId is scoped per
/// research now, not globally unique, and this login form still takes only a bare id with no
/// research to disambiguate with). Callers must check this before <see cref="ParticipantExists"/>
/// and fail loud instead of silently resolving whichever match came first.
/// </param>
/// <param name="NotApplicable">
/// True when the participant's research isn't a PR-review research (<c>Research.TaskType !=
/// 'PR_REVIEW'</c>) — this app only ever runs the PR-review flow, so a participant whose bare id
/// happens to resolve against a Google-Forms/Generic-typed research is refused here rather than
/// incorrectly started on an Intro/AI/Report flow that was never meant for their study. Defense in
/// depth — a participant on a non-PR-review research is never told to open this app in normal
/// operation.
/// </param>
/// <param name="TimerMinutes">
/// Per-app participant timer (2026-09-11), from <c>Research.TimerCodeReviewEnabled/Minutes</c> —
/// <c>null</c> when disabled or not applicable. Only ever set on a genuine "here's your next
/// session" result, never on the error/blocked paths (nothing to time until a session actually
/// starts).
/// </param>
/// <param name="IsTestParticipant">
/// <c>Participant.IsTestParticipant</c> — a fixed, repeatable-use participant that can log in via
/// the bare-id form instead of a personal link, bypasses consent/progression, and never has
/// <c>IsFinished</c> flipped for it. Only ever <c>true</c> on the "here's your next session"
/// result (a test participant is never blocked by consent/all-finished, so those paths are moot).
/// </param>
/// <param name="BaselineRequired">
/// True when the next unfinished session is the participant's FIRST experimental session (the
/// one at <c>ParticipantSession.SequenceOrder = 2</c> — AI or Report, whichever comes first for
/// this participant's own counterbalancing order; never Intro) and the researcher hasn't yet
/// marked the baseline (EEG) measurement done (<c>Participant.BaselineDoneAt IS NULL</c>). Same
/// blocking shape as <see cref="ConsentRequired"/> — checked after it, also bypassed entirely for
/// test participants. Baseline is recorded manually by the researcher in the Admin Dashboard
/// (there is nothing participant-facing to do about it), so unlike <see cref="ConsentRequired"/>
/// the participant-facing banner for this has no external link, just a "wait for your
/// researcher" message.
/// </param>
public record StudyLoginState(
    bool ParticipantExists,
    int? SessionId,
    string? SessionName,
    bool AllFinished,
    bool ConsentRequired,
    string? Language,
    bool AmbiguousParticipantId = false,
    bool NotApplicable = false,
    int? TimerMinutes = null,
    bool IsTestParticipant = false,
    bool BaselineRequired = false);

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
    /// Resolves a Code Review personal-link token (<c>SurveyAccessToken.SurveyType = 'CODE_REVIEW'</c>,
    /// minted by the Admin Dashboard) to the participant id it belongs to. Returns <c>null</c> for an
    /// unknown or expired token — never throws. This is the only way a non-test participant can
    /// resolve their identity; the bare-id login form is test-participants-only.
    /// </summary>
    Task<string?> ResolveParticipantIdByLinkTokenAsync(string token, CancellationToken ct);

    /// <summary>
    /// Mints a short-lived (<c>SurveyAccessToken.SurveyType = 'TLX_HANDOFF'</c>) token carrying this
    /// participant's id and the study session they just finished, so the NASA-TLX handoff URL can
    /// carry an opaque token instead of a raw participant id + session number. Returns <c>null</c>
    /// on any DB failure (logged, never thrown) — the caller falls back to the legacy query-param
    /// handoff shape in that case rather than stranding the participant.
    /// </summary>
    Task<string?> IssueTlxHandoffTokenAsync(string participantId, int sessionId, CancellationToken ct);

    /// <summary>
    /// Deletes a test participant's previous <c>ChatMessage</c>/<c>HybridSectionEngagement</c> rows
    /// for this exact session before a fresh <c>start-review</c> — the append-only tables that
    /// wouldn't otherwise self-overwrite the way <c>ReviewDecision</c>/<c>TlxResult</c>/
    /// <c>ActivityLog</c> already do, so a test participant repeating the same session keeps only
    /// their latest run instead of accumulating every prior attempt. Best-effort: failures are
    /// logged internally and never thrown. Never call this for a real (non-test) participant.
    /// </summary>
    Task ResetTestParticipantSessionDataAsync(string participantId, int sessionId, CancellationToken ct);

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

    /// <summary>
    /// Records one expand/collapse event for a Hybrid-mode documentation accordion section.
    /// Best-effort: failures are logged internally and never thrown. Only ever called for
    /// <see cref="ReviewMode.Hybrid"/> sessions — Ai/Report sessions never write here.
    /// </summary>
    Task SaveHybridSectionEventAsync(
        string participantId,
        int sessionId,
        string sectionId,
        string sectionTitle,
        HybridSectionAction action,
        int? durationSeconds,
        CancellationToken ct);

    /// <summary>
    /// Reads the session's finished activity-log CSV off disk and stores it in the shared study
    /// database, so the researcher can read it in the Admin Dashboard instead of having to reach
    /// the machine the backend ran on. Upserts on (participant, session) — this is called both
    /// when the decision is submitted and again when the session is torn down, so a later call
    /// simply replaces the earlier, shorter snapshot.
    /// <para>
    /// Best-effort in the same sense as every other study-persistence path here: no-ops silently
    /// when activity logging is disabled (<paramref name="filePath"/> is <c>null</c>) or the file
    /// is gone, and any DB/IO failure is logged, never thrown.
    /// </para>
    /// </summary>
    Task SaveActivityLogAsync(
        string? filePath,
        string participantId,
        int sessionId,
        ReviewMode mode,
        CancellationToken ct);
}
