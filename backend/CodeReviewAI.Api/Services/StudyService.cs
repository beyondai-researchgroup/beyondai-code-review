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
    private static readonly Dictionary<int, string> SessionNamesById = new() { [1] = "Intro", [2] = "AI", [3] = "Report" };

    private readonly Lazy<NpgsqlDataSource> _dataSource;
    private readonly HashSet<string> _testParticipantIds;
    private readonly Dictionary<string, int> _testParticipantFixedSessions;
    private readonly ILogger<StudyService> _logger;

    /// <summary>Creates the service; the data source is initialised lazily on first use.</summary>
    public StudyService(IConfiguration configuration, ILogger<StudyService> logger)
    {
        _logger = logger;
        _dataSource = new Lazy<NpgsqlDataSource>(() =>
        {
            var url = configuration["Study:DatabaseUrl"];
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException(
                    "Study:DatabaseUrl is not configured. Set it via user-secrets to the shared Neon connection URL.");
            return NpgsqlDataSource.Create(ToConnectionString(url));
        });

        _testParticipantIds = new HashSet<string>(
            configuration.GetSection("Study:TestParticipantIds").Get<string[]>() ?? [],
            StringComparer.Ordinal);

        // Optional per-participant pin (id -> SessionId) for test participants that should
        // always land on a specific session instead of the default Intro — e.g. a participant
        // dedicated to repeatedly testing just the AI mode, or just the Report mode.
        _testParticipantFixedSessions = configuration.GetSection("Study:TestParticipantFixedSessions").Get<Dictionary<string, int>>()
            ?? new Dictionary<string, int>();
    }

    /// <inheritdoc />
    public async Task<StudyLoginState> GetLoginStateAsync(string participantId, CancellationToken ct)
    {
        await using var conn = await _dataSource.Value.OpenConnectionAsync(ct);

        await using (var existsCmd = new NpgsqlCommand(
            """SELECT 1 FROM "Participant" WHERE "ParticipantId" = @pid LIMIT 1""", conn))
        {
            existsCmd.Parameters.AddWithValue("pid", participantId);
            if (await existsCmd.ExecuteScalarAsync(ct) is null)
                return new StudyLoginState(false, null, null, false);
        }

        // Test participants (Study:TestParticipantIds) always land on the same fixed session —
        // Intro by default, or whichever session Study:TestParticipantFixedSessions pins them
        // to — regardless of their actual ParticipantSession.IsFinished flags. NASA-TLX still
        // updates those flags and still records TlxResult normally; the override only affects
        // what this login read-back returns, so the pinned session can be replayed indefinitely.
        if (_testParticipantIds.Contains(participantId))
        {
            var sessionId = _testParticipantFixedSessions.GetValueOrDefault(participantId, 1);
            return new StudyLoginState(true, sessionId, SessionNamesById[sessionId], false);
        }

        await using var nextCmd = new NpgsqlCommand(
            """
            SELECT ps."SessionId", s."Name"
            FROM "ParticipantSession" ps
            JOIN "Sessions" s ON s."Id" = ps."SessionId"
            WHERE ps."ParticipantId" = @pid AND ps."IsFinished" = FALSE
            ORDER BY ps."SessionId"
            LIMIT 1
            """, conn);
        nextCmd.Parameters.AddWithValue("pid", participantId);

        await using var reader = await nextCmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new StudyLoginState(true, null, null, true);

        return new StudyLoginState(true, reader.GetInt32(0), reader.GetString(1), false);
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
                ON CONFLICT ("ParticipantId", "SessionId") DO UPDATE SET
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

    /// <summary>
    /// Converts a postgres:// URI (Neon format) into an Npgsql keyword connection string.
    /// Neon requires TLS, so SSL Mode=Require is always set.
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
            SslMode = SslMode.Require
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
