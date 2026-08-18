using CodeReviewAI.Api.Exceptions;
using CodeReviewAI.Api.Models;
using CodeReviewAI.Api.Services;

namespace CodeReviewAI.Api.Endpoints;

/// <summary>Request body for a study login attempt.</summary>
/// <param name="ParticipantId">Participant identifier from the shared Participant table.</param>
public record StudyLoginRequest(string ParticipantId);

/// <summary>Request body for starting a review with the preconfigured demo PR.</summary>
/// <param name="ParticipantId">Participant identifier (revalidated server-side).</param>
/// <param name="ReviewMode">The mode to open: AI chat or written report.</param>
/// <param name="Lang">UI language code (<c>sr</c> or <c>en</c>). Defaults to <c>sr</c>.</param>
public record StartReviewRequest(string ParticipantId, ReviewMode ReviewMode, string Lang = "sr");

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
        group.MapPost("/start-review", StartReview);

        return app;
    }

    // ── POST /api/study/login ────────────────────────────────────────────────

    /// <summary>
    /// Validates the participant id and returns their next unfinished session
    /// (Intro → AI → Report). Session order and completion are tracked in the
    /// shared ParticipantSession table that the NASA-TLX app updates on completion.
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

        if (!state.ParticipantExists)
            return Results.NotFound(new { error = "Participant not found.", detail = participantId });

        // Informed consent (given via the separate Consent app) is a hard prerequisite for the
        // whole study flow, same as AllFinished blocks it at the other end — the frontend shows
        // a message + link to the Consent app instead of the mode-choice screen.
        if (state.ConsentRequired)
            return Results.Ok(new { allFinished = false, consentRequired = true });

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
            sessionId = state.SessionId,
            sessionName = state.SessionName,
            // Locked once at the Consent app and propagated here — the frontend applies this
            // instead of letting the participant pick a language on this screen.
            language = state.Language
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
        CancellationToken ct)
    {
        var participantId = body.ParticipantId?.Trim() ?? string.Empty;
        if (participantId.Length is 0 or > 50)
            return Results.BadRequest(new { error = "Invalid participant id.", detail = "Participant id is required." });

        var state = await study.GetLoginStateAsync(participantId, ct);
        if (!state.ParticipantExists)
            return Results.NotFound(new { error = "Participant not found.", detail = participantId });
        if (state.ConsentRequired)
            return Results.BadRequest(new { error = "Consent required.", detail = "This participant hasn't completed the informed-consent step yet." });
        if (state.AllFinished)
            return Results.BadRequest(new { error = "All sessions finished.", detail = "This participant has no remaining sessions." });

        // Prefer this session's explicit PR override, then the research's active config, then
        // fall back to the legacy global Study:Pr:*/GitHub:PersonalAccessToken config when the
        // research has none configured (e.g. pre-existing test participants backfilled to the
        // seeded Test Research). state.SessionId is non-null here — AllFinished was already
        // checked above.
        var prConfig = await study.GetPrConfigForParticipantAsync(participantId, state.SessionId!.Value, ct);

        string? owner, repo, token;
        int prNumber;
        if (prConfig is not null)
        {
            owner = prConfig.Owner;
            repo = prConfig.Repo;
            prNumber = prConfig.PrNumber;
            token = prConfig.Token;
        }
        else
        {
            owner = config["Study:Pr:Owner"];
            repo = config["Study:Pr:Repo"];
            prNumber = config.GetValue<int>("Study:Pr:Number");
            token = config["GitHub:PersonalAccessToken"];
        }

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) ||
            prNumber <= 0 || string.IsNullOrWhiteSpace(token))
        {
            return Results.Json(
                new { error = "Demo PR not configured.", detail = "The participant's Research has no PR config, and the fallback Study:Pr:Owner/Repo/Number + GitHub:PersonalAccessToken is not set either." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

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
            session.LastActivityAt = DateTime.UtcNow;
            await sessions.UpdateSessionAsync(session);

            await eeg.MarkerAsync(body.ReviewMode == ReviewMode.Ai ? "AI_START" : "REPORT_START", ct);

            return Results.Ok(new
            {
                sessionId = session.Id,
                summary = ReviewSessionEndpoints.ToPrSummary(prContext, shortSummary)
            });
        }
        catch (GitHubIntegrationException ex)
        {
            return Results.BadRequest(new { error = "GitHub error", detail = ex.Message });
        }
    }
}
