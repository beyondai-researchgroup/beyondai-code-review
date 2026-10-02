using CodeReviewAI.Api.Exceptions;
using CodeReviewAI.Api.Models;
using CodeReviewAI.Api.Services;

namespace CodeReviewAI.Api.Endpoints;

/// <summary>Request body for a study login attempt (test participants only — see <see cref="LinkLoginRequest"/>).</summary>
/// <param name="ParticipantId">Participant identifier from the shared Participant table.</param>
public record StudyLoginRequest(string ParticipantId);

/// <summary>Request body for a personal-link login (every non-test participant).</summary>
/// <param name="Token">The opaque token from the participant's personal Code Review link.</param>
public record LinkLoginRequest(string Token);

/// <summary>Request body for starting a review with the preconfigured demo PR.</summary>
/// <param name="ParticipantId">Participant identifier (revalidated server-side).</param>
/// <param name="ReviewMode">The mode to open: AI chat or written report.</param>
/// <param name="Lang">UI language code (<c>sr</c> or <c>en</c>). Defaults to <c>sr</c>.</param>
/// <param name="LinkToken">
/// The personal-link token this participant logged in with — required (and re-validated) for
/// every non-test participant, so a client can't start a review just by guessing/replaying a
/// participant id without ever holding a valid link. Ignored for test participants.
/// </param>
public record StartReviewRequest(string ParticipantId, ReviewMode ReviewMode, string Lang = "sr", string? LinkToken = null);

/// <summary>
/// Registers the <c>/api/study</c> route group — the participant-facing study flow.
/// Participants log in with just their id; the PR under review is preconfigured
/// (<c>Study:Pr:*</c> + <c>GitHub:PersonalAccessToken</c>), so no GitHub data is
/// ever entered by, or exposed to, the participant.
/// </summary>
internal static class StudyEndpoints
{
    /// <summary>Adds all study endpoints to the application.</summary>
    internal static WebApplication MapStudyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/study");

        group.MapPost("/login", Login);
        group.MapPost("/link-login", LinkLogin);
        group.MapPost("/start-review", StartReview);

        return app;
    }

    // ── POST /api/study/login ────────────────────────────────────────────────

    /// <summary>
    /// Validates the participant id and returns their next unfinished session
    /// (Intro → AI → Report). Session order and completion are tracked in the
    /// shared ParticipantSession table that the NASA-TLX app updates on completion.
    /// Bare-id login is test-participants-only — see <see cref="LinkLogin"/> for everyone else.
    /// </summary>
    private static async Task<IResult> Login(
        StudyLoginRequest body,
        IStudyService study,
        IEegControlService eeg,
        CancellationToken ct)
    {
        var participantId = body.ParticipantId?.Trim() ?? string.Empty;
        if (participantId.Length is 0 or > 50)
            return Results.BadRequest(new { error = "Invalid participant id.", detail = "Participant id is required." });

        var state = await study.GetLoginStateAsync(participantId, ct);

        if (state.AmbiguousParticipantId)
            return Results.Conflict(new { error = "AMBIGUOUS_PARTICIPANT_ID", detail = "This participant id exists in more than one research and cannot be resolved from a bare id alone." });

        if (!state.ParticipantExists)
            return Results.NotFound(new { error = "Participant not found.", detail = participantId });

        if (state.NotApplicable)
            return Results.BadRequest(new { error = "NOT_APPLICABLE", detail = "This participant's research doesn't use the PR-review flow." });

        if (!state.IsTestParticipant)
            return Results.Json(
                new { error = "PERSONAL_LINK_REQUIRED", detail = "This participant can only access the study via their personal link." },
                statusCode: StatusCodes.Status403Forbidden);

        return await BuildLoginResponse(participantId, state, eeg, ct);
    }

    // ── POST /api/study/link-login ───────────────────────────────────────────

    /// <summary>
    /// Resolves a personal-link token (minted by the Admin Dashboard) to a participant and logs
    /// them in exactly like <see cref="Login"/> — this is the only entry point for every
    /// non-test participant.
    /// </summary>
    private static async Task<IResult> LinkLogin(
        LinkLoginRequest body,
        IStudyService study,
        IEegControlService eeg,
        CancellationToken ct)
    {
        var token = body.Token?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(token))
            return Results.BadRequest(new { error = "Invalid token.", detail = "Token is required." });

        var participantId = await study.ResolveParticipantIdByLinkTokenAsync(token, ct);
        if (participantId is null)
            return Results.Json(
                new { error = "LINK_INVALID", detail = "This link is invalid or has expired." },
                statusCode: StatusCodes.Status410Gone);

        var state = await study.GetLoginStateAsync(participantId, ct);
        if (state.AmbiguousParticipantId)
            return Results.Conflict(new { error = "AMBIGUOUS_PARTICIPANT_ID", detail = "This participant id exists in more than one research and cannot be resolved from a bare id alone." });
        if (!state.ParticipantExists)
            return Results.NotFound(new { error = "Participant not found.", detail = participantId });
        if (state.NotApplicable)
            return Results.BadRequest(new { error = "NOT_APPLICABLE", detail = "This participant's research doesn't use the PR-review flow." });

        return await BuildLoginResponse(participantId, state, eeg, ct);
    }

    /// <summary>Shared consent/all-finished/EEG-start/response-shape logic for both login paths.</summary>
    private static async Task<IResult> BuildLoginResponse(string participantId, StudyLoginState state, IEegControlService eeg, CancellationToken ct)
    {
        // Informed consent (given via the separate Consent app) is a hard prerequisite for the
        // whole study flow, same as AllFinished blocks it at the other end — the frontend shows
        // a message + link to the Consent app instead of the mode-choice screen.
        if (state.ConsentRequired)
            return Results.Ok(new { allFinished = false, consentRequired = true });

        // First experimental session (SequenceOrder = 2) is blocked until the researcher has
        // marked the baseline (EEG) measurement done in the Admin Dashboard — nothing
        // participant-facing to do about it, so the frontend shows a plain "wait for your
        // researcher" message instead of the mode-choice screen.
        if (state.BaselineRequired)
            return Results.Ok(new { allFinished = false, baselineRequired = true });

        if (state.AllFinished)
            return Results.Ok(new { allFinished = true });

        // Intro (session 1) is the first login of the study — (re)start EEG recording.
        // Any later login (returning for session 2/3) just resumes after the between-session pause.
        if (state.SessionId == 1)
            await eeg.StartAsync(participantId, ct);
        else
            await eeg.ResumeAsync(ct);

        return Results.Ok(new
        {
            allFinished = false,
            // The frontend needs this to call start-review — Login's caller already knows it (they
            // typed it), but LinkLogin's caller only ever held an opaque token, never the id itself.
            participantId,
            sessionId = state.SessionId,
            sessionName = state.SessionName,
            // Locked once at the Consent app and propagated here — the frontend applies this
            // instead of letting the participant pick a language on this screen.
            language = state.Language,
            // Per-app participant timer (2026-09-11) — null when disabled. The frontend actually
            // starts the countdown once the review itself opens (see StartReview's own response
            // below), not on this mode-choice screen; returned here too just for shape parity.
            timerMinutes = state.TimerMinutes,
            isTestParticipant = state.IsTestParticipant
        });
    }

    // ── POST /api/study/start-review ─────────────────────────────────────────

    /// <summary>
    /// Creates a review session and loads the preconfigured demo PR into it in the
    /// requested mode. The participant is revalidated so a stale/forged client state
    /// cannot start a review for a finished or unknown participant.
    /// </summary>
    private static async Task<IResult> StartReview(
        StartReviewRequest body,
        IStudyService study,
        ISessionService sessions,
        IRemoteRepositoryService github,
        IContextManagerService contextManager,
        IClaudeService claude,
        IConfiguration config,
        IEegControlService eeg,
        IActivityLogService activityLog,
        CancellationToken ct)
    {
        var participantId = body.ParticipantId?.Trim() ?? string.Empty;
        if (participantId.Length is 0 or > 50)
            return Results.BadRequest(new { error = "Invalid participant id.", detail = "Participant id is required." });

        var state = await study.GetLoginStateAsync(participantId, ct);
        if (state.AmbiguousParticipantId)
            return Results.Conflict(new { error = "AMBIGUOUS_PARTICIPANT_ID", detail = "This participant id exists in more than one research and cannot be resolved from a bare id alone." });
        if (!state.ParticipantExists)
            return Results.NotFound(new { error = "Participant not found.", detail = participantId });
        if (state.NotApplicable)
            return Results.BadRequest(new { error = "NOT_APPLICABLE", detail = "This participant's research doesn't use the PR-review flow." });
        if (state.ConsentRequired)
            return Results.BadRequest(new { error = "Consent required.", detail = "This participant hasn't completed the informed-consent step yet." });
        if (state.BaselineRequired)
            return Results.BadRequest(new { error = "Baseline required.", detail = "The researcher hasn't marked the baseline measurement done for this participant yet." });
        if (state.AllFinished)
            return Results.BadRequest(new { error = "All sessions finished.", detail = "This participant has no remaining sessions." });

        // Defense in depth alongside Login/LinkLogin's own gate: a non-test participant must
        // present the same personal-link token they logged in with, re-validated against this
        // exact participant, so starting a review is never possible from just a guessed/replayed
        // participant id without ever holding a valid link.
        if (!state.IsTestParticipant)
        {
            var linkToken = body.LinkToken?.Trim();
            var tokenParticipantId = string.IsNullOrEmpty(linkToken)
                ? null
                : await study.ResolveParticipantIdByLinkTokenAsync(linkToken, ct);
            if (tokenParticipantId is null || !string.Equals(tokenParticipantId, participantId, StringComparison.Ordinal))
                return Results.Json(
                    new { error = "PERSONAL_LINK_REQUIRED", detail = "A valid personal-link token for this participant is required." },
                    statusCode: StatusCodes.Status403Forbidden);
        }
        else
        {
            // Test participants only ever keep their LATEST run of a given session — clear out the
            // previous attempt's append-only chat/hybrid-engagement rows before this fresh one
            // starts writing (ReviewDecision/TlxResult/ActivityLog already upsert on their own).
            await study.ResetTestParticipantSessionDataAsync(participantId, state.SessionId!.Value, ct);
        }

        // Explicit per-session PrConfigId if the Admin Dashboard's Excel import assigned one,
        // otherwise (Intro session only) the research's single IsIntro task. No further fallback —
        // an AI/Report session with nothing explicitly assigned (or an Intro session in a research
        // with no IsIntro task configured) fails loud below instead of silently opening some other
        // PR. state.SessionId is non-null here — AllFinished was already checked above.
        var prConfig = await study.GetPrConfigForParticipantAsync(participantId, state.SessionId!.Value, ct);

        if (prConfig is null)
        {
            return Results.Json(
                new { error = "PR not configured.", detail = "This participant's session has no PR assigned — check the Excel import's Task column, or (for the Intro session) that the research has a task flagged as Intro in Task Configuration." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        var owner = prConfig.Owner;
        var repo = prConfig.Repo;
        var prNumber = prConfig.PrNumber;
        var token = prConfig.Token;

        try
        {
            var (prContext, shortSummary, docsContent) = await ReviewSessionEndpoints.FetchPrWithSummaryAsync(
                github, contextManager, claude, config, token, owner, repo, prNumber, body.Lang, ct);

            var session = await sessions.CreateSessionAsync();
            session.PrContext = prContext;
            session.ShortSummary = shortSummary;
            session.DocsContent = docsContent;
            session.GitHubToken = token;
            session.Owner = owner;
            session.Repo = repo;
            session.Mode = body.ReviewMode;
            session.ParticipantId = participantId;
            session.StudySessionId = state.SessionId!.Value;
            session.IsTestParticipant = state.IsTestParticipant;
            session.ActivityLogFilePath = activityLog.CreateLog(session.Id, participantId, state.SessionId!.Value, body.ReviewMode);
            session.LastActivityAt = DateTime.UtcNow;
            await sessions.UpdateSessionAsync(session);

            var startMarker = body.ReviewMode switch
            {
                ReviewMode.Ai => "AI_START",
                ReviewMode.Report => "REPORT_START",
                ReviewMode.Hybrid => "HYBRID_START",
                _ => "AI_START"
            };
            await eeg.MarkerAsync(startMarker, ct);

            return Results.Ok(new
            {
                sessionId = session.Id,
                summary = ReviewSessionEndpoints.ToPrSummary(prContext, shortSummary),
                // Per-app participant timer (2026-09-11) — the actual countdown starts once this
                // review session is open, so this is the response the frontend reads it from.
                timerMinutes = state.TimerMinutes
            });
        }
        catch (GitHubIntegrationException ex)
        {
            return Results.BadRequest(new { error = "GitHub error", detail = ex.Message });
        }
    }
}
