using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services;

/// <inheritdoc cref="IActivityLogService" />
internal sealed class ActivityLogService : IActivityLogService
{
    private const string HeaderRow = "StartedAt,EndedAt,ParticipantId,StudySessionId,Mode,EventType,Detail";

    private readonly ConcurrentDictionary<string, StringBuilder> _buffers = new();
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(ILogger<ActivityLogService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string CreateLog(string sessionId, string participantId, int studySessionId, ReviewMode mode)
    {
        var logId = Guid.NewGuid().ToString("N");
        var sb = new StringBuilder();
        sb.Append(HeaderRow).Append('\n');
        _buffers[logId] = sb;

        LogEvent(logId, participantId, studySessionId, mode, "SessionStarted", null, DateTime.UtcNow, DateTime.UtcNow);
        return logId;
    }

    /// <inheritdoc />
    public void LogEvent(
        string? logId,
        string participantId,
        int studySessionId,
        ReviewMode mode,
        string eventType,
        string? detail,
        DateTime startedAt,
        DateTime? endedAt)
    {
        if (logId is null) return;
        if (!_buffers.TryGetValue(logId, out var sb))
        {
            _logger.LogWarning("Activity log {LogId} not found (already released?); dropping row {EventType}", logId, eventType);
            return;
        }

        var row = string.Join(',',
            Csv(startedAt.ToString("O", CultureInfo.InvariantCulture)),
            Csv(endedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(participantId),
            Csv(studySessionId.ToString(CultureInfo.InvariantCulture)),
            Csv(mode.ToString()),
            Csv(eventType),
            Csv(detail ?? string.Empty));

        // One StringBuilder per session, appended from whatever request happens to be handling
        // that session at the time — a lock per buffer (not a single global lock) so concurrent
        // activity across different sessions doesn't serialize against each other.
        lock (sb)
        {
            sb.Append(row).Append('\n');
        }
    }

    /// <inheritdoc />
    public string? GetContent(string? logId)
    {
        if (logId is null) return null;
        if (!_buffers.TryGetValue(logId, out var sb)) return null;
        lock (sb)
        {
            return sb.ToString();
        }
    }

    /// <inheritdoc />
    public void ReleaseLog(string? logId)
    {
        if (logId is null) return;
        _buffers.TryRemove(logId, out _);
    }

    /// <summary>Quotes a CSV field only when it actually needs it (contains a comma, quote, or newline).</summary>
    private static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
