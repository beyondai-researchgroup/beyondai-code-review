using System.Text;
using System.Text.Json;
using CodeReviewAI.Api.Exceptions;
using CodeReviewAI.Api.Models;
using CodeReviewAI.Api.Models.Requests;
using CodeReviewAI.Api.Models.Responses;
using CodeReviewAI.Api.Services;

namespace CodeReviewAI.Api.Endpoints;

/// <summary>
/// Registers the <c>/api/session</c> route group with all review-session endpoints.
/// </summary>
internal static class ReviewSessionEndpoints
{
    /// <summary>Maximum accepted length of a single chat message, in characters.</summary>
    private const int MaxChatMessageLength = 8_000;

    /// <summary>Maximum accepted length of a review-decision comment, in characters.</summary>
    private const int MaxDecisionCommentLength = 2_000;

    /// <summary>Adds all review session endpoints to the application.</summary>
    internal static WebApplication MapReviewSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/session");

        group.MapPost("/", CreateSession);
        group.MapPost("/{sessionId}/load-pr", LoadPr);
        group.MapPost("/{sessionId}/load-repo-context", LoadRepoContext);
        group.MapGet("/{sessionId}/pr-summary", GetPrSummary);
        group.MapGet("/{sessionId}/file-patch", GetFilePatch);
        group.MapPost("/{sessionId}/chat/stream", ChatStream);
        group.MapPost("/{sessionId}/chat/suggestions", GetChatSuggestions);
        group.MapPost("/{sessionId}/report/generate", GenerateReport);
        group.MapPost("/{sessionId}/mode", SwitchMode);
        group.MapPost("/{sessionId}/decision", SubmitDecision);
        group.MapPost("/{sessionId}/hybrid/section-event", RecordHybridSectionEvent);
        group.MapPost("/{sessionId}/activity-log", RecordActivity);
        group.MapDelete("/{sessionId}", DeleteSession);

        return app;
    }

    // ── POST /api/session ────────────────────────────────────────────────────

    /// <summary>Creates a new empty review session and returns its identifier.</summary>
    private static async Task<IResult> CreateSession(ISessionService sessions)
    {
        var session = await sessions.CreateSessionAsync();
        return Results.Ok(new SessionCreatedResponse(session.Id));
    }

    // ── POST /api/session/{sessionId}/load-pr ────────────────────────────────

    /// <summary>Loads a GitHub PR into an existing session.</summary>
    private static async Task<IResult> LoadPr(
        string sessionId,
        LoadPrRequest body,
        ISessionService sessions,
        IRemoteRepositoryService github,
        IContextManagerService contextManager,
        IClaudeService claude,
        IConfiguration config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.GitHubToken) ||
            string.IsNullOrWhiteSpace(body.Owner) ||
            string.IsNullOrWhiteSpace(body.Repo) ||
            body.PrNumber <= 0)
        {
            return Results.BadRequest(new
            {
                error = "Invalid request",
                detail = "GitHubToken, Owner, Repo and a positive PrNumber are all required."
            });
        }

        var session = await sessions.GetSessionAsync(sessionId);
        if (session is null)
            return Results.NotFound(new { error = "Session not found.", detail = sessionId });

        try
        {
            var (prContext, shortSummary, docsContent) = await FetchPrWithSummaryAsync(
                github, contextManager, claude, config,
                body.GitHubToken, body.Owner, body.Repo, body.PrNumber, body.Lang, ct);

            session.PrContext = prContext;
            session.ShortSummary = shortSummary;
            session.DocsContent = docsContent;
            session.GitHubToken = body.GitHubToken;
            session.Owner = body.Owner;
            session.Repo = body.Repo;
            session.Mode = body.ReviewMode;
            session.LastActivityAt = DateTime.UtcNow;
            await sessions.UpdateSessionAsync(session);

            return Results.Ok(ToPrSummary(prContext, shortSummary));
        }
        catch (GitHubIntegrationException ex)
        {
            return Results.BadRequest(new { error = "GitHub error", detail = ex.Message });
        }
    }

    // ── POST /api/session/{sessionId}/load-repo-context ─────────────────────

    /// <summary>
    /// Fetches the repository file tree and key file contents and stores them in the session
    /// so subsequent chat turns include full project context.
    /// </summary>
    private static async Task<IResult> LoadRepoContext(
        string sessionId,
        ISessionService sessions,
        IRemoteRepositoryService github,
        CancellationToken ct)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session is null)
            return Results.NotFound(new { error = "Session not found.", detail = sessionId });

        if (session.GitHubToken is null || session.Owner is null || session.Repo is null)
            return Results.BadRequest(new { error = "PR must be loaded before loading repository context." });

        try
        {
            var repoContext = await github.LoadRepoContextAsync(
                session.GitHubToken, session.Owner, session.Repo, ct);

            session.RepoContext = repoContext;
            session.LastActivityAt = DateTime.UtcNow;
            await sessions.UpdateSessionAsync(session);

            return Results.Ok(new
            {
                loaded = true,
                charCount = repoContext.Length
            });
        }
        catch (GitHubIntegrationException ex)
        {
            return Results.BadRequest(new { error = "GitHub error", detail = ex.Message });
        }
    }

    // ── GET /api/session/{sessionId}/pr-summary ──────────────────────────────

    /// <summary>Returns the PR summary for an already-loaded session.</summary>
    private static async Task<IResult> GetPrSummary(
        string sessionId,
        ISessionService sessions)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session?.PrContext is null)
            return Results.NotFound(new { error = "Session not found or PR not yet loaded.", detail = sessionId });

        return Results.Ok(ToPrSummary(session.PrContext, session.ShortSummary));
    }

    // ── GET /api/session/{sessionId}/file-patch?fileName=... ────────────────

    /// <summary>Returns the unified diff patch for a single file in the loaded PR.</summary>
    private static async Task<IResult> GetFilePatch(
        string sessionId,
        string fileName,
        ISessionService sessions)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session?.PrContext is null)
            return Results.NotFound(new { error = "Session not found or PR not loaded.", detail = sessionId });

        var file = session.PrContext.Files.FirstOrDefault(f => f.FileName == fileName);
        if (file is null)
            return Results.NotFound(new { error = "File not found in PR.", detail = fileName });

        return Results.Ok(new { patch = file.Patch });
    }

    // ── POST /api/session/{sessionId}/chat/stream ────────────────────────────

    /// <summary>
    /// Streams a Claude AI response as Server-Sent Events.
    /// The handler returns <see cref="Task"/> (not <see cref="IResult"/>) so that
    /// ASP.NET does not attempt to write a result after we have already started
    /// streaming the response body.
    /// </summary>
    private static async Task ChatStream(
        string sessionId,
        ChatRequest body,
        ISessionService sessions,
        IContextManagerService contextManager,
        IClaudeService claude,
        IStudyService study,
        IActivityLogService activityLog,
        IConfiguration config,
        HttpContext http,
        CancellationToken ct)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            await http.Response.WriteAsJsonAsync(new { error = "Session not found.", detail = sessionId }, ct);
            return;
        }

        if (session.PrContext is null)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { error = "PR not loaded for this session." }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(body.Message) || body.Message.Length > MaxChatMessageLength)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new
            {
                error = "Invalid message.",
                detail = $"Message must be non-empty and at most {MaxChatMessageLength} characters."
            }, ct);
            return;
        }

        activityLog.LogEvent(
            session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
            "ChatMessageSent", body.Message, DateTime.UtcNow, DateTime.UtcNow);

        // Rate limiting (user questions only) and the history mutation happen atomically
        // under the session lock, so two concurrent requests can neither both slip past
        // the limit nor corrupt the History list.
        var maxPerHour = config.GetValue<int>("Session:MaxMessagesPerHour", 30);
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var userMessage = new ChatMessage("user", body.Message, DateTime.UtcNow);
        List<ApiMessage> apiMessages;

        lock (session.Sync)
        {
            var recentCount = session.History.Count(m => m.Role == "user" && m.Timestamp >= cutoff);
            if (recentCount >= maxPerHour)
            {
                apiMessages = [];
            }
            else
            {
                // Experimental Hybrid mode only — feeds the AI the same documentation content the
                // participant sees alongside chat, so it can reference a specific section back.
                // Mirrors exactly what the documentation pane itself renders: the static demo doc
                // when that override is on (always true for Hybrid mode locally), else whatever
                // was last generated for this session (null if nothing yet — block just omitted).
                string? hybridDocContent = session.Mode == ReviewMode.Hybrid
                    ? (config.GetValue<bool>("Session:UseStaticReport")
                        ? StaticReportContent.Get(body.Lang)
                        : session.GeneratedReport)
                    : null;

                // Build messages before mutating history to avoid duplicating the current question.
                apiMessages = contextManager.BuildMessages(session.PrContext, session.History, body.Message, session.RepoContext, session.DocsContent, body.Lang, hybridDocContent);

                // Persist the user turn immediately so it survives a mid-stream disconnect.
                session.History.Add(userMessage);
                session.LastActivityAt = DateTime.UtcNow;
            }
        }

        if (apiMessages.Count == 0)
        {
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await http.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded.",
                detail = $"Maximum {maxPerHour} messages per hour per session."
            }, ct);
            return;
        }

        await sessions.UpdateSessionAsync(session);

        if (session.ParticipantId is not null && session.StudySessionId is not null)
        {
            await study.SaveChatMessageAsync(
                session.ParticipantId, session.StudySessionId.Value, "user", body.Message,
                CancellationToken.None);
        }

        // Switch to SSE mode — no turning back after this point.
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var fullResponse = new StringBuilder();
        var aiResponseStartedAt = DateTime.UtcNow;
        try
        {
            await foreach (var chunk in claude.StreamResponseAsync(apiMessages, ct, ClaudeService.GetSystemPrompt(body.Lang)))
            {
                var payload = JsonSerializer.Serialize(new { text = chunk });
                await http.Response.WriteAsync($"data: {payload}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
                fullResponse.Append(chunk);
            }

            await http.Response.WriteAsync("data: [DONE]\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected mid-stream — stop writing; persist whatever was collected.
        }
        finally
        {
            // Runs even when the Claude call itself throws (e.g. ClaudeApiException).
            // Append the assistant turn only if we got a non-empty response. On a completely
            // failed stream, retract the user turn as well — otherwise the history keeps a
            // dangling question that (a) counts against the rate limit and (b) gets re-sent
            // as a duplicate consecutive user message on the next turn.
            lock (session.Sync)
            {
                if (fullResponse.Length > 0)
                    session.History.Add(new ChatMessage("assistant", fullResponse.ToString(), DateTime.UtcNow));
                else
                    session.History.Remove(userMessage);
            }
            await sessions.UpdateSessionAsync(session);

            if (fullResponse.Length > 0 && session.ParticipantId is not null && session.StudySessionId is not null)
            {
                await study.SaveChatMessageAsync(
                    session.ParticipantId, session.StudySessionId.Value, "assistant", fullResponse.ToString(),
                    CancellationToken.None);
            }

            if (fullResponse.Length > 0)
            {
                activityLog.LogEvent(
                    session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
                    "ChatMessageReceived", fullResponse.ToString(), aiResponseStartedAt, DateTime.UtcNow);
            }
        }
    }

    // ── POST /api/session/{sessionId}/chat/suggestions ──────────────────────

    /// <summary>
    /// Asks Claude to generate 4 contextual follow-up question suggestions based
    /// on the current PR and conversation history.
    /// Returns a JSON object with a <c>suggestions</c> string array.
    /// </summary>
    private static async Task<IResult> GetChatSuggestions(
        string sessionId,
        string? lang,
        ISessionService sessions,
        IContextManagerService contextManager,
        IClaudeService claude,
        CancellationToken ct)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session?.PrContext is null)
            return Results.NotFound(new { error = "Session not found or PR not yet loaded.", detail = sessionId });

        var messages = contextManager.BuildSuggestionsMessages(session.PrContext, session.History, lang ?? "sr");

        var sb = new StringBuilder();
        await foreach (var chunk in claude.StreamResponseAsync(messages, ct, ClaudeService.GetSystemPrompt(lang ?? "sr")))
            sb.Append(chunk);

        var raw = sb.ToString().Trim();

        // Claude sometimes wraps the JSON in a markdown code block — strip it.
        if (raw.StartsWith("```"))
        {
            var start = raw.IndexOf('[');
            var end = raw.LastIndexOf(']');
            if (start >= 0 && end > start)
                raw = raw[start..(end + 1)];
        }

        try
        {
            var suggestions = JsonSerializer.Deserialize<string[]>(raw);
            return Results.Ok(new { suggestions = suggestions ?? [] });
        }
        catch
        {
            return Results.Ok(new { suggestions = Array.Empty<string>() });
        }
    }

    // ── POST /api/session/{sessionId}/report/generate ───────────────────────

    /// <summary>
    /// Streams an AI report for a Report-mode session as Server-Sent Events.
    /// If a cached report exists and <c>?regenerate=true</c> is not set, the cached
    /// text is returned as a single SSE event so the client-side code is uniform.
    /// Pass <c>?regenerate=true</c> to force a new generation.
    /// </summary>
    private static async Task GenerateReport(
        string sessionId,
        bool? regenerate,
        string? lang,
        ISessionService sessions,
        IContextManagerService contextManager,
        IClaudeService claude,
        IActivityLogService activityLog,
        IConfiguration config,
        HttpContext http,
        CancellationToken ct)
    {
        var reportRequestStartedAt = DateTime.UtcNow;
        var session = await sessions.GetSessionAsync(sessionId);
        if (session is null)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            await http.Response.WriteAsJsonAsync(new { error = "Session not found.", detail = sessionId }, ct);
            return;
        }

        if (session.Mode != ReviewMode.Report && session.Mode != ReviewMode.Hybrid)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { error = "This session is not in Report mode." }, ct);
            return;
        }

        if (session.PrContext is null)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { error = "PR not loaded for this session." }, ct);
            return;
        }

        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        // Intro session (StudySessionId 1) always gets the short companion doc, regardless of
        // Session:UseStaticReport — a first-time participant needs something they can realistically
        // read start to finish during the guided tour, not the full multi-thousand-word report
        // real Report/Hybrid sessions serve. Takes priority over every other branch below.
        if (session.StudySessionId == 1)
        {
            var introDoc = StaticIntroReportContent.Get(lang ?? "sr");
            var introPayload = JsonSerializer.Serialize(new { text = introDoc });
            await http.Response.WriteAsync($"data: {introPayload}\n\n", ct);
            await http.Response.WriteAsync("data: [DONE]\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
            activityLog.LogEvent(
                session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
                "DocumentationGenerated", $"intro-static, {introDoc.Length} chars", reportRequestStartedAt, DateTime.UtcNow);
            return;
        }

        // Test/demo override — serves a predefined technical document instead of calling Claude.
        // The dynamic AI generation path below is left intact; flip Session:UseStaticReport off
        // in configuration to restore live report generation.
        if (config.GetValue<bool>("Session:UseStaticReport"))
        {
            var staticDoc = StaticReportContent.Get(lang ?? "sr");
            var staticPayload = JsonSerializer.Serialize(new { text = staticDoc });
            await http.Response.WriteAsync($"data: {staticPayload}\n\n", ct);
            await http.Response.WriteAsync("data: [DONE]\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
            activityLog.LogEvent(
                session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
                "DocumentationGenerated", $"static, {staticDoc.Length} chars", reportRequestStartedAt, DateTime.UtcNow);
            return;
        }

        // Cached path — return the stored report as a single chunk so the client code is uniform.
        if (session.GeneratedReport is not null && regenerate != true)
        {
            var cached = JsonSerializer.Serialize(new { text = session.GeneratedReport });
            await http.Response.WriteAsync($"data: {cached}\n\n", ct);
            await http.Response.WriteAsync("data: [DONE]\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
            activityLog.LogEvent(
                session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
                "DocumentationGenerated", $"cached, {session.GeneratedReport.Length} chars", reportRequestStartedAt, DateTime.UtcNow);
            return;
        }

        var resolvedLang = lang ?? "sr";
        var messages = contextManager.BuildReportMessages(session.PrContext, resolvedLang);
        var fullReport = new StringBuilder();

        try
        {
            await foreach (var chunk in claude.StreamResponseAsync(messages, ct, ClaudeService.GetReportSystemPrompt(resolvedLang)))
            {
                var payload = JsonSerializer.Serialize(new { text = chunk });
                await http.Response.WriteAsync($"data: {payload}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
                fullReport.Append(chunk);
            }

            await http.Response.WriteAsync("data: [DONE]\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }

        if (fullReport.Length > 0)
        {
            session.GeneratedReport = fullReport.ToString();
            session.LastActivityAt = DateTime.UtcNow;
            await sessions.UpdateSessionAsync(session);
            activityLog.LogEvent(
                session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
                "DocumentationGenerated", $"AI-generated, {fullReport.Length} chars", reportRequestStartedAt, DateTime.UtcNow);
        }
    }

    // ── POST /api/session/{sessionId}/mode ───────────────────────────────────

    /// <summary>
    /// Switches a session's review mode in place (AI ↔ Report), reusing the same PR context.
    /// Restricted to Intro (study session 1) sessions — the self-guided tour uses this to show
    /// both modes live on one demo PR. Real AI/Report study sessions must keep the mode they
    /// were assigned, so the comparison between modes stays valid.
    /// </summary>
    private static async Task<IResult> SwitchMode(
        string sessionId,
        SwitchModeRequest body,
        ISessionService sessions)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session is null)
            return Results.NotFound(new { error = "Session not found.", detail = sessionId });

        if (session.StudySessionId != 1)
            return Results.BadRequest(new { error = "Mode switching is only allowed for Intro sessions." });

        if (!Enum.IsDefined(typeof(ReviewMode), body.Mode))
            return Results.BadRequest(new { error = "Invalid mode." });

        session.Mode = body.Mode;
        session.LastActivityAt = DateTime.UtcNow;
        await sessions.UpdateSessionAsync(session);

        return Results.Ok(new { mode = session.Mode });
    }

    // ── POST /api/session/{sessionId}/decision ───────────────────────────────

    /// <summary>
    /// Records the human reviewer's own decision (Accept / Reject) and a required comment.
    /// This endpoint does not call Claude or GitHub. For study-flow sessions the decision
    /// is additionally persisted to the shared study database (best-effort). The AI never
    /// influences or sets this decision.
    /// </summary>
    private static async Task<IResult> SubmitDecision(
        string sessionId,
        SubmitDecisionRequest body,
        ISessionService sessions,
        IStudyService study,
        IEegControlService eeg,
        IActivityLogService activityLog)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session is null)
            return Results.NotFound(new { error = "Session not found.", detail = sessionId });

        if (!Enum.IsDefined(typeof(ReviewDecisionType), body.Decision))
            return Results.BadRequest(new { error = "Nevažeća vrednost za polje Decision." });

        if (string.IsNullOrWhiteSpace(body.Comment))
            return Results.BadRequest(new { error = "Komentar je obavezan.", detail = "Komentar je obavezan." });

        if (body.Comment.Length > MaxDecisionCommentLength)
            return Results.BadRequest(new
            {
                error = "Komentar je predugačak.",
                detail = $"Komentar može imati najviše {MaxDecisionCommentLength} karaktera."
            });

        var decision = new ReviewDecision
        {
            Decision = body.Decision,
            Comment = body.Comment.Trim(),
            DecidedAt = DateTime.UtcNow
        };

        session.Decision = decision;
        session.LastActivityAt = DateTime.UtcNow;
        await sessions.UpdateSessionAsync(session);

        if (session.ParticipantId is not null && session.StudySessionId is not null)
        {
            await study.SaveDecisionAsync(
                session.ParticipantId, session.StudySessionId.Value,
                session.Mode, decision.Decision, decision.Comment,
                CancellationToken.None);
        }

        await eeg.MarkerAsync("DECISION", CancellationToken.None);

        activityLog.LogEvent(
            session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
            "DecisionSubmitted", $"{decision.Decision}: {decision.Comment}", decision.DecidedAt, decision.DecidedAt);

        // Persist the finished log to the shared study DB. Deliberately after the LogEvent above,
        // so the decision row itself is part of what gets stored. Best-effort, like every other
        // study-persistence call here; DeleteSession upserts over this again on teardown.
        if (session.ParticipantId is not null && session.StudySessionId is not null)
        {
            await study.SaveActivityLogAsync(
                session.ActivityLogFilePath, session.ParticipantId, session.StudySessionId.Value,
                session.Mode, CancellationToken.None);
        }

        // A real (non-test) participant gets an opaque NASA-TLX handoff token instead of their
        // raw participant id/session number in the redirect URL — null for a test participant
        // (no personal link to speak of; the frontend falls back to the legacy query-param shape)
        // and best-effort null on any DB failure (see IssueTlxHandoffTokenAsync).
        string? handoffToken = null;
        if (!session.IsTestParticipant && session.ParticipantId is not null && session.StudySessionId is not null)
        {
            handoffToken = await study.IssueTlxHandoffTokenAsync(session.ParticipantId, session.StudySessionId.Value, CancellationToken.None);
        }

        return Results.Ok(new { decision.Decision, decision.Comment, decision.DecidedAt, handoffToken });
    }

    // ── POST /api/session/{sessionId}/hybrid/section-event ──────────────────

    /// <summary>
    /// Records one expand/collapse event (and, on collapse, the dwell duration) for a Hybrid-mode
    /// documentation accordion section — best-effort, persisted to the shared study database.
    /// The frontend only ever calls this for Hybrid sessions (gated by ReportViewComponent's own
    /// `trackEngagement` input — Report mode is sectioned too now but never sets it); no
    /// server-side mode check is needed since a stray call from another mode is harmless (just an
    /// extra best-effort row keyed by whatever StudySessionId the session actually has).
    /// </summary>
    private static async Task<IResult> RecordHybridSectionEvent(
        string sessionId,
        HybridSectionEventRequest body,
        ISessionService sessions,
        IStudyService study,
        IActivityLogService activityLog)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session is null)
            return Results.NotFound(new { error = "Session not found.", detail = sessionId });

        if (string.IsNullOrWhiteSpace(body.SectionId) || string.IsNullOrWhiteSpace(body.SectionTitle))
            return Results.BadRequest(new { error = "SectionId and SectionTitle are required." });

        if (session.ParticipantId is not null && session.StudySessionId is not null)
        {
            await study.SaveHybridSectionEventAsync(
                session.ParticipantId, session.StudySessionId.Value,
                body.SectionId, body.SectionTitle, body.Action, body.DurationSeconds,
                CancellationToken.None);
        }

        // Also mirrored into the general activity-log CSV (unlike the DB write above, this applies
        // to section engagement in ANY mode — Report mode is sectioned too now but has no DB row).
        var now = DateTime.UtcNow;
        var startedAt = body.DurationSeconds.HasValue ? now.AddSeconds(-body.DurationSeconds.Value) : now;
        activityLog.LogEvent(
            session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
            $"Section{body.Action}", body.SectionTitle, startedAt, now);

        return Results.Ok();
    }

    // ── POST /api/session/{sessionId}/activity-log ───────────────────────────

    /// <summary>
    /// Records one general participant activity entry (a click, a resize, a search, ...) to the
    /// session's activity-log CSV file — best-effort, local-dev-only (see
    /// <see cref="IActivityLogService"/>). No-ops (still 200 OK) when activity logging is disabled
    /// or this session has no study context.
    /// </summary>
    private static async Task<IResult> RecordActivity(
        string sessionId,
        ActivityLogRequest body,
        ISessionService sessions,
        IActivityLogService activityLog,
        IEegControlService eeg)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session is null)
            return Results.NotFound(new { error = "Session not found.", detail = sessionId });

        if (string.IsNullOrWhiteSpace(body.EventType))
            return Results.BadRequest(new { error = "EventType is required." });

        activityLog.LogEvent(
            session.ActivityLogFilePath, session.ParticipantId ?? "", session.StudySessionId ?? 0, session.Mode,
            body.EventType, body.Detail, body.StartedAt ?? DateTime.UtcNow, body.EndedAt);

        // Client-side moments that also drop a phase marker into the EEG recording — the client keeps
        // sending one event stream, and only this whitelisted mapping ever reaches the EEG app.
        if (EegMarkerForActivity.TryGetValue(body.EventType, out var eegMarker))
            await eeg.MarkerAsync(eegMarker, CancellationToken.None);

        return Results.Ok();
    }

    private static readonly Dictionary<string, string> EegMarkerForActivity = new()
    {
        // Participant clicked "Donesi odluku", i.e. started deciding (DECISION follows on submit).
        ["FinishModalOpened"] = "DECISION_OPENED"
    };

    // ── DELETE /api/session/{sessionId} ──────────────────────────────────────

    /// <summary>Deletes a session immediately.</summary>
    private static async Task<IResult> DeleteSession(
        string sessionId,
        ISessionService sessions,
        IActivityLogService activityLog,
        IStudyService study)
    {
        var session = await sessions.GetSessionAsync(sessionId);
        if (session is not null && session.ActivityLogFilePath is not null && session.ParticipantId is not null)
        {
            activityLog.LogEvent(
                session.ActivityLogFilePath, session.ParticipantId, session.StudySessionId ?? 0, session.Mode,
                "SessionEnded", null, DateTime.UtcNow, DateTime.UtcNow);

            // Final persist of the complete log, including the SessionEnded row just written.
            // This is the path that also catches a participant who never submitted a decision
            // (abandoned or timed out), since SubmitDecision's own call never ran for them.
            // Upserts over whatever SubmitDecision already stored, by design.
            if (session.StudySessionId is not null)
            {
                await study.SaveActivityLogAsync(
                    session.ActivityLogFilePath, session.ParticipantId, session.StudySessionId.Value,
                    session.Mode, CancellationToken.None);
            }

            // Session is genuinely done now — free the in-memory CSV buffer. Safe even if
            // SaveActivityLogAsync above threw internally (it never rethrows), since the content
            // either made it to Postgres/R2 or didn't; either way nothing else will ever append
            // to this log again.
            activityLog.ReleaseLog(session.ActivityLogFilePath);
        }

        await sessions.DeleteSessionAsync(sessionId);
        return Results.NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a PR from GitHub and generates the short AI description summary.
    /// Shared by the classic load-pr endpoint and the study start-review flow.
    /// Summary failures are non-fatal — the summary stays null and the UI shows a fallback.
    /// Also silently fetches the repo's Docs/ folder (if any) as hidden chat reference
    /// material — a fetch failure there is likewise non-fatal.
    /// </summary>
    internal static async Task<(PrContext Pr, string? ShortSummary, string? DocsContent)> FetchPrWithSummaryAsync(
        IRemoteRepositoryService github,
        IContextManagerService contextManager,
        IClaudeService claude,
        IConfiguration config,
        string token,
        string owner,
        string repo,
        int prNumber,
        string lang,
        CancellationToken ct)
    {
        var prContext = await github.LoadPrContextAsync(token, owner, repo, prNumber, ct);

        // Test/demo override — every study participant must see identical wording, so the
        // fixed text below is used instead of a fresh (non-deterministic) AI paraphrase.
        // The dynamic AI generation path is left intact; flip Session:UseStaticSummary off
        // in configuration to restore live summary generation.
        string? shortSummary;
        if (config.GetValue<bool>("Session:UseStaticSummary"))
        {
            shortSummary = StaticSummaryContent.Get(lang);
        }
        else
        {
            shortSummary = null;
            var summaryMessages = contextManager.BuildShortSummaryMessages(prContext.Description, lang);
            if (summaryMessages is not null)
            {
                try
                {
                    var sb = new StringBuilder();
                    await foreach (var chunk in claude.StreamResponseAsync(summaryMessages, ct, ClaudeService.GetSystemPrompt(lang)))
                        sb.Append(chunk);
                    shortSummary = sb.ToString().Trim();
                }
                catch { /* non-fatal */ }
            }
        }

        string? docsContent = null;
        try
        {
            docsContent = await github.LoadDocsContentAsync(token, owner, repo, ct);
        }
        catch { /* non-fatal — hidden reference material, never required for the session */ }

        return (prContext, shortSummary, docsContent);
    }

    internal static PrSummaryResponse ToPrSummary(PrContext pr, string? shortSummary) =>
        new(
            pr.Title,
            pr.Author,
            pr.BaseBranch,
            pr.HeadBranch,
            pr.Description,
            pr.Files.Select(f => new PrFileSummary(f.FileName, f.Status, f.Additions, f.Deletions)).ToList(),
            pr.Files.Sum(f => f.Additions),
            pr.Files.Sum(f => f.Deletions),
            shortSummary);
}
