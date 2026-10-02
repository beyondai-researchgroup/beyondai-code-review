using CodeReviewAI.Api.Models;
using Npgsql;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Npgsql-backed implementation of <see cref="IStudyService"/> against the shared
/// Neon Postgres database (same one the NASA-TLX app uses). The connection URL is
/// read from <c>Study:DatabaseUrl</c> (postgres:// URI form, as issued by Neon).
/// </summary>
internal sealed class StudyService : IStudyService, IAsyncDisposable
{
    private static readonly Dictionary<int, string> SessionNamesById = new() { [1] = "Intro", [2] = "AI", [3] = "Report", [4] = "Hybrid" };

    private readonly Lazy<NpgsqlDataSource> _dataSource;
    private readonly IActivityLogService _activityLog;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<StudyService> _logger;

    /// <summary>Creates the service; the data source is initialised lazily on first use.</summary>
    public StudyService(
        IConfiguration configuration,
        IActivityLogService activityLog,
        IBlobStorageService blobStorage,
        ILogger<StudyService> logger)
    {
        _activityLog = activityLog;
        _blobStorage = blobStorage;
        _logger = logger;
        _dataSource = new Lazy<NpgsqlDataSource>(() =>
        {
            var url = configuration["Study:DatabaseUrl"];
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException(
                    "Study:DatabaseUrl is not configured. Set it via user-secrets to the shared Neon connection URL.");
            return NpgsqlDataSource.Create(ToConnectionString(url));
        });
    }

    /// <inheritdoc />
    public async Task<StudyLoginState> GetLoginStateAsync(string participantId, CancellationToken ct)
    {
        await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);

        string? language;
        bool consentGiven;
        bool usesConsentForm;
        string taskType;
        int? timerMinutes;
        bool isTestParticipant;
        int? testFixedSessionId;
        bool baselineDone;
        await using (var infoCmd = new NpgsqlCommand(
            """
            SELECT p."Language", p."ConsentGivenAt", COALESCE(r."UsesConsentForm", TRUE), COALESCE(r."TaskType", 'PR_REVIEW'),
                   CASE WHEN r."TimerCodeReviewEnabled" THEN r."TimerCodeReviewMinutes" ELSE NULL END,
                   p."IsTestParticipant", p."TestFixedSessionId", p."BaselineDoneAt"
            FROM "Participant" p
            LEFT JOIN "Research" r ON r."Id" = p."ResearchId"
            WHERE p."ParticipantId" = @pid
            """, conn))
        {
            infoCmd.Parameters.AddWithValue("pid", participantId);
            await using var infoReader = await infoCmd.ExecuteReaderAsync(ct);
            if (!await infoReader.ReadAsync(ct))
                return new StudyLoginState(false, null, null, false, false, null);

            language = await infoReader.IsDBNullAsync(0, ct) ? null : infoReader.GetString(0);
            consentGiven = !await infoReader.IsDBNullAsync(1, ct);
            usesConsentForm = infoReader.GetBoolean(2);
            taskType = infoReader.GetString(3);
            timerMinutes = await infoReader.IsDBNullAsync(4, ct) ? null : infoReader.GetInt32(4);
            isTestParticipant = infoReader.GetBoolean(5);
            testFixedSessionId = await infoReader.IsDBNullAsync(6, ct) ? null : infoReader.GetInt32(6);
            baselineDone = !await infoReader.IsDBNullAsync(7, ct);

            // Item 1 of the "platform improvements round 2" plan — ParticipantId is scoped per
            // research now, not globally unique, and this login form still takes only a bare id
            // with no research to disambiguate with. A second matching row means this specific
            // id genuinely collides across two researches — fail loud instead of silently using
            // whichever row happened to come back first.
            if (await infoReader.ReadAsync(ct))
                return new StudyLoginState(false, null, null, false, false, null, AmbiguousParticipantId: true);
        }

        // This app only ever runs the PR-review flow (Intro/AI/Report + EEG) — those are specific
        // to a PR_REVIEW research, not generic platform primitives. A participant whose research
        // is Google-Forms/Generic-typed is refused here rather than incorrectly started on a flow
        // that was never meant for their study. Defense in depth: such a participant is never told
        // to open this app in normal operation.
        if (!string.Equals(taskType, "PR_REVIEW", StringComparison.OrdinalIgnoreCase))
            return new StudyLoginState(true, null, null, false, false, language, NotApplicable: true);

        // Fixed test participants (Participant.IsTestParticipant + TestFixedSessionId) always
        // land on the one session TestFixedSessionId pins them to — regardless of their actual
        // ParticipantSession.IsFinished flags, and regardless of consent status. NASA-TLX still
        // updates IsFinished/TlxResult normally for a REAL participant; for a test participant it
        // deliberately does not (see the NASA-TLX side of this change) — the whole point of this
        // override is letting them repeatedly exercise the same flow without ever "using it up".
        if (isTestParticipant && testFixedSessionId is not null)
        {
            var sessionId = testFixedSessionId.Value;
            return new StudyLoginState(true, sessionId, SessionNamesById[sessionId], false, false, language, IsTestParticipant: true, TimerMinutes: timerMinutes);
        }

        // A test participant WITHOUT a fixed session is a "cycling" one (e.g. 005): they walk their
        // own ParticipantSession rows in order (Intro → AI → Report, with whatever PR each row
        // assigns), and once every row is finished, all rows are reset so the next login starts
        // over at the first one. Consent and baseline are still skipped, like every test participant.
        // NASA-TLX flips IsFinished for this kind of test participant (unlike fixed ones).
        if (isTestParticipant)
        {
            var next = await GetNextUnfinishedSessionAsync(conn, participantId, ct);
            if (next is null)
            {
                await using var resetCmd = new NpgsqlCommand(
                    """
                    UPDATE "ParticipantSession" SET "IsFinished" = FALSE, "FinishedAt" = NULL
                    WHERE "ParticipantId" = @pid
                    """, conn);
                resetCmd.Parameters.AddWithValue("pid", participantId);
                await resetCmd.ExecuteNonQueryAsync(ct);
                next = await GetNextUnfinishedSessionAsync(conn, participantId, ct);
            }

            // No ParticipantSession rows at all — fall back to Intro, the old default.
            var (cycleSessionId, cycleSessionName) = next is null
                ? (1, SessionNamesById[1])
                : (next.Value.SessionId, next.Value.Name);
            return new StudyLoginState(true, cycleSessionId, cycleSessionName, false, false, language, IsTestParticipant: true, TimerMinutes: timerMinutes);
        }

        if (usesConsentForm && !consentGiven)
            return new StudyLoginState(true, null, null, false, true, language);

        await using var nextCmd = new NpgsqlCommand(
            """
            SELECT ps."SessionId", s."Name", ps."SequenceOrder"
            FROM "ParticipantSession" ps
            JOIN "Sessions" s ON s."Id" = ps."SessionId"
            WHERE ps."ParticipantId" = @pid AND ps."IsFinished" = FALSE
            ORDER BY COALESCE(ps."SequenceOrder", ps."SessionId")
            LIMIT 1
            """, conn);
        nextCmd.Parameters.AddWithValue("pid", participantId);

        await using var reader = await nextCmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new StudyLoginState(true, null, null, true, false, language);

        var nextSessionId = reader.GetInt32(0);
        var nextSessionName = reader.GetString(1);
        var sequenceOrder = await reader.IsDBNullAsync(2, ct) ? (int?)null : reader.GetInt32(2);

        // "First experimental session" is SequenceOrder = 2 — not a fixed SessionId, since AI/Report
        // can be swapped per participant for counterbalancing (Excel import order). Same effective-
        // order computation as this query's own ORDER BY (COALESCE(SequenceOrder, SessionId)) and
        // the one admin-dashboard-andrejkatin's overview endpoint already uses.
        var effectiveSequence = sequenceOrder ?? nextSessionId;
        if (effectiveSequence == 2 && !baselineDone)
            return new StudyLoginState(true, null, null, false, false, language, BaselineRequired: true);

        return new StudyLoginState(true, nextSessionId, nextSessionName, false, false, language, TimerMinutes: timerMinutes);
    }

    /// <summary>
    /// The participant's first unfinished study session in their own order
    /// (<c>COALESCE(SequenceOrder, SessionId)</c>), or null when every row is finished or none exist.
    /// </summary>
    private static async Task<(int SessionId, string Name)?> GetNextUnfinishedSessionAsync(
        NpgsqlConnection conn, string participantId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT ps."SessionId", s."Name"
            FROM "ParticipantSession" ps
            JOIN "Sessions" s ON s."Id" = ps."SessionId"
            WHERE ps."ParticipantId" = @pid AND ps."IsFinished" = FALSE
            ORDER BY COALESCE(ps."SequenceOrder", ps."SessionId")
            LIMIT 1
            """, conn);
        cmd.Parameters.AddWithValue("pid", participantId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetInt32(0), reader.GetString(1));
    }

    /// <inheritdoc />
    public async Task<PrConfig?> GetPrConfigForParticipantAsync(string participantId, int sessionId, CancellationToken ct)
    {
        await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);
        // "explicit" = this participant+session's own PrConfigId override, if any (set via the
        // Admin Dashboard's Excel import, by task label). "introCfg" = the research's single task
        // flagged IsIntro — only ever relevant for session 1 (Intro never gets an explicit
        // PrConfigId; the Excel import deliberately never assigns one there). COALESCE per column
        // so an explicit override always wins when present. There is no other fallback: an AI/
        // Report session with no explicit assignment (and Intro when the research has no IsIntro
        // task configured) resolves to null, which the caller (StudyEndpoints.StartReview) turns
        // into a clear "PR not configured" error instead of silently picking some other PR.
        // Joined on ParticipantGuid (not the bare, per-research-scoped ParticipantId) — resolved
        // from ParticipantId via the shared set_participant_guid-populated Participant.Guid.
        await using var cmd = new NpgsqlCommand(
            """
            SELECT COALESCE(explicitCfg."GitHubOwner", introCfg."GitHubOwner"),
                   COALESCE(explicitCfg."GitHubRepo", introCfg."GitHubRepo"),
                   COALESCE(explicitCfg."GitHubPrNumber", introCfg."GitHubPrNumber"),
                   COALESCE(explicitCfg."GitHubToken", introCfg."GitHubToken")
            FROM "Participant" p
            JOIN "ParticipantSession" ps ON ps."ParticipantGuid" = p."Guid" AND ps."SessionId" = @sid
            LEFT JOIN "ResearchPrConfig" explicitCfg ON explicitCfg."Id" = ps."PrConfigId"
            LEFT JOIN "ResearchPrConfig" introCfg
                ON introCfg."ResearchId" = p."ResearchId" AND introCfg."IsIntro" = TRUE AND ps."SessionId" = 1
            WHERE p."ParticipantId" = @pid
            """, conn);
        cmd.Parameters.AddWithValue("pid", participantId);
        cmd.Parameters.AddWithValue("sid", sessionId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || await reader.IsDBNullAsync(0, ct))
            return null;

        return new PrConfig(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3));
    }

    /// <inheritdoc />
    public async Task<string?> ResolveParticipantIdByLinkTokenAsync(string token, CancellationToken ct)
    {
        await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT p."ParticipantId"
            FROM "SurveyAccessToken" sat
            JOIN "Participant" p ON p."Guid" = sat."ParticipantGuid"
            WHERE sat."Token" = @token AND sat."SurveyType" = 'CODE_REVIEW' AND sat."ExpiresAt" > NOW()
            """, conn);
        cmd.Parameters.AddWithValue("token", token);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    /// <inheritdoc />
    public async Task<string?> IssueTlxHandoffTokenAsync(string participantId, int sessionId, CancellationToken ct)
    {
        try
        {
            await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);
            var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO "SurveyAccessToken" ("ParticipantId", "ParticipantGuid", "SurveyType", "Token", "ExpiresAt", "StudySessionId")
                SELECT p."ParticipantId", p."Guid", 'TLX_HANDOFF', @token, NOW() + INTERVAL '2 hours', @sid
                FROM "Participant" p WHERE p."ParticipantId" = @pid
                ON CONFLICT ("ParticipantGuid", "SurveyType") DO UPDATE SET
                    "Token" = EXCLUDED."Token", "ExpiresAt" = EXCLUDED."ExpiresAt",
                    "StudySessionId" = EXCLUDED."StudySessionId", "CreatedAt" = NOW()
                """, conn);
            cmd.Parameters.AddWithValue("token", token);
            cmd.Parameters.AddWithValue("sid", sessionId);
            cmd.Parameters.AddWithValue("pid", participantId);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0 ? token : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue TLX handoff token for participant {ParticipantId}, session {SessionId}", participantId, sessionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task ResetTestParticipantSessionDataAsync(string participantId, int sessionId, CancellationToken ct)
    {
        try
        {
            await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(
                """
                DELETE FROM "ChatMessage" WHERE "ParticipantGuid" = (SELECT "Guid" FROM "Participant" WHERE "ParticipantId" = @pid) AND "SessionId" = @sid;
                DELETE FROM "HybridSectionEngagement" WHERE "ParticipantGuid" = (SELECT "Guid" FROM "Participant" WHERE "ParticipantId" = @pid) AND "SessionId" = @sid;
                """, conn);
            cmd.Parameters.AddWithValue("pid", participantId);
            cmd.Parameters.AddWithValue("sid", sessionId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset test-participant session data for {ParticipantId}, session {SessionId}", participantId, sessionId);
        }
    }

    /// <inheritdoc />
    public async Task SaveDecisionAsync(
        string participantId,
        int sessionId,
        ReviewMode reviewMode,
        ReviewDecisionType decision,
        string comment,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO "ReviewDecision" ("ParticipantId", "SessionId", "ReviewMode", "Decision", "Comment")
                VALUES (@pid, @sid, @mode, @decision, @comment)
                -- Conflict target moved from (ParticipantId, SessionId) to (ParticipantGuid,
                -- SessionId) -- item 1 of admin-dashboard-andrejkatin's "platform improvements
                -- round 2" plan. ParticipantGuid is auto-populated by a shared-DB trigger from
                -- ParticipantId before the conflict check runs (also fails loud with a clear
                -- Postgres exception if this ParticipantId is ambiguous across researches).
                ON CONFLICT ("ParticipantGuid", "SessionId") DO UPDATE SET
                    "ReviewMode" = EXCLUDED."ReviewMode",
                    "Decision" = EXCLUDED."Decision",
                    "Comment" = EXCLUDED."Comment",
                    "DecidedAt" = NOW()
                """, conn);
            cmd.Parameters.AddWithValue("pid", participantId);
            cmd.Parameters.AddWithValue("sid", sessionId);
            cmd.Parameters.AddWithValue("mode", reviewMode.ToString());
            cmd.Parameters.AddWithValue("decision", decision.ToString());
            cmd.Parameters.AddWithValue("comment", comment);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save review decision for participant {ParticipantId}, session {SessionId}", participantId, sessionId);
        }
    }

    /// <inheritdoc />
    public async Task SaveChatMessageAsync(
        string participantId,
        int sessionId,
        string role,
        string content,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO "ChatMessage" ("ParticipantId", "SessionId", "Role", "Content")
                VALUES (@pid, @sid, @role, @content)
                """, conn);
            cmd.Parameters.AddWithValue("pid", participantId);
            cmd.Parameters.AddWithValue("sid", sessionId);
            cmd.Parameters.AddWithValue("role", role);
            cmd.Parameters.AddWithValue("content", content);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save chat message for participant {ParticipantId}, session {SessionId}", participantId, sessionId);
        }
    }

    /// <inheritdoc />
    public async Task SaveHybridSectionEventAsync(
        string participantId,
        int sessionId,
        string sectionId,
        string sectionTitle,
        HybridSectionAction action,
        int? durationSeconds,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO "HybridSectionEngagement"
                    ("ParticipantId", "SessionId", "SectionId", "SectionTitle", "Action", "DurationSeconds")
                VALUES (@pid, @sid, @secid, @sectitle, @action, @duration)
                """, conn);
            cmd.Parameters.AddWithValue("pid", participantId);
            cmd.Parameters.AddWithValue("sid", sessionId);
            cmd.Parameters.AddWithValue("secid", sectionId);
            cmd.Parameters.AddWithValue("sectitle", sectionTitle);
            cmd.Parameters.AddWithValue("action", action.ToString());
            cmd.Parameters.AddWithValue("duration", (object?)durationSeconds ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save hybrid section event for participant {ParticipantId}, session {SessionId}, section {SectionId}", participantId, sessionId, sectionId);
        }
    }

    /// <inheritdoc />
    public async Task SaveActivityLogAsync(
        string? logId,
        string participantId,
        int sessionId,
        ReviewMode mode,
        CancellationToken ct)
    {
        // Activity logging never actually failed to start now (ActivityLogService.CreateLog is
        // always enabled), but logId can still be null for the handful of call sites that run
        // before a log exists (defensive, matches every other best-effort guard in this file).
        var rawCsv = _activityLog.GetContent(logId);
        if (rawCsv is null)
        {
            _logger.LogWarning(
                "Activity log {LogId} has no content; nothing persisted for participant {ParticipantId}, session {SessionId}",
                logId, participantId, sessionId);
            return;
        }

        try
        {
            // Data rows only — the header row doesn't count, and a trailing newline shouldn't
            // inflate the total.
            var rowCount = Math.Max(
                0,
                rawCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1);

            var originalFilename = $"{SanitizeForFilename(participantId)}_{mode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            // Same R2-or-DB split as every upload type in admin-dashboard-andrejkatin's server/storage/r2.mjs
            // (this is the real research instrument the user asked to keep working once hosted —
            // Postgres alone already survives hosting fine, R2 is the extra durable copy they asked
            // for specifically). "RawCsv" stays populated when R2 isn't configured — a developer
            // machine with no R2 credentials behaves exactly as before this feature existed.
            string? storageKey = null;
            string? dbRawCsv = rawCsv;
            if (_blobStorage.IsConfigured)
            {
                try
                {
                    storageKey = await _blobStorage.PutTextAsync(
                        IBlobStorageService.BuildKey("activity-logs", participantId, originalFilename),
                        rawCsv, "text/csv", ct);
                    dbRawCsv = null;
                }
                catch (Exception ex)
                {
                    // R2 upload failed — fall back to storing the CSV in Postgres itself rather than
                    // losing the research data entirely for this session.
                    _logger.LogError(ex, "R2 upload failed for activity log, participant {ParticipantId}, session {SessionId}; falling back to DB storage", participantId, sessionId);
                    storageKey = null;
                    dbRawCsv = rawCsv;
                }
            }

            await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO "ActivityLog"
                    ("ParticipantId", "SessionId", "ReviewMode", "OriginalFilename", "RawCsv", "RowCount", "StorageKey")
                VALUES (@pid, @sid, @mode, @filename, @csv, @rows, @storageKey)
                ON CONFLICT ("ParticipantGuid", "SessionId") DO UPDATE SET
                    "ReviewMode"       = EXCLUDED."ReviewMode",
                    "OriginalFilename" = EXCLUDED."OriginalFilename",
                    "RawCsv"           = EXCLUDED."RawCsv",
                    "RowCount"         = EXCLUDED."RowCount",
                    "StorageKey"       = EXCLUDED."StorageKey",
                    "SavedAt"          = NOW()
                """, conn);
            cmd.Parameters.AddWithValue("pid", participantId);
            cmd.Parameters.AddWithValue("sid", sessionId);
            cmd.Parameters.AddWithValue("mode", mode.ToString());
            cmd.Parameters.AddWithValue("filename", originalFilename);
            cmd.Parameters.AddWithValue("csv", (object?)dbRawCsv ?? DBNull.Value);
            cmd.Parameters.AddWithValue("rows", rowCount);
            cmd.Parameters.AddWithValue("storageKey", (object?)storageKey ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogInformation(
                "Saved activity log ({RowCount} rows, storage={Storage}) for participant {ParticipantId}, session {SessionId}",
                rowCount, storageKey is null ? "postgres" : "r2", participantId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save activity log for participant {ParticipantId}, session {SessionId}", participantId, sessionId);
        }
    }

    /// <summary>Keeps a participant id safe to use as part of a filename/object key.</summary>
    private static string SanitizeForFilename(string participantId)
    {
        var safe = new string(participantId.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return safe.Length == 0 ? "unknown" : safe;
    }

    /// <summary>
    /// Converts a postgres:// URI (Neon format, or a local Postgres URL for "developer mode" —
    /// see docs/local-dev-database.md in admin-dashboard-andrejkatin, the DB this app shares with
    /// the rest of the platform) into an Npgsql keyword connection string. Neon requires TLS,
    /// so SSL Mode=Require is used for it; a loopback host (the local Postgres Docker container)
    /// has no TLS configured at all, so SSL is disabled for it instead — same
    /// uri.IsLoopback-based distinction this codebase's CORS config already uses elsewhere.
    /// </summary>
    internal static string ToConnectionString(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = uri.IsLoopback ? SslMode.Disable : SslMode.Require
        };
        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_dataSource.IsValueCreated)
            await _dataSource.Value.DisposeAsync();
    }
}
