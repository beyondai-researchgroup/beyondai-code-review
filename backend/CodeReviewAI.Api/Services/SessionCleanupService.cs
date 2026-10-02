namespace CodeReviewAI.Api.Services;

/// <summary>
/// Hosted background service that evicts expired sessions every 10 minutes.
/// The session TTL is read from <c>Session:TimeoutMinutes</c> in configuration.
/// </summary>
internal sealed class SessionCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    private readonly SessionService _sessions;
    private readonly IConfiguration _configuration;
    private readonly IActivityLogService _activityLog;
    private readonly IStudyService _study;
    private readonly ILogger<SessionCleanupService> _logger;

    /// <summary>Creates the cleanup service.</summary>
    /// <param name="sessions">
    /// The singleton session store. Injected as the concrete type so the
    /// internal <c>RemoveExpiredSessions</c> method is accessible.
    /// </param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="activityLog">Writes the final "SessionExpired" row before the log is stored.</param>
    /// <param name="study">Persists the evicted session's activity log to the shared study DB.</param>
    /// <param name="logger">Logs persistence failures — never rethrown, this must not kill the loop.</param>
    public SessionCleanupService(
        SessionService sessions,
        IConfiguration configuration,
        IActivityLogService activityLog,
        IStudyService study,
        ILogger<SessionCleanupService> logger)
    {
        _sessions = sessions;
        _configuration = configuration;
        _activityLog = activityLog;
        _study = study;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CleanupInterval, stoppingToken);

            var timeoutMinutes = _configuration.GetValue<int>("Session:TimeoutMinutes", 120);
            var evicted = _sessions.RemoveExpiredSessions(TimeSpan.FromMinutes(timeoutMinutes));

            // A participant who simply closed the browser never triggers SubmitDecision or
            // DeleteSession, so this sweep is the only place their activity log ever reaches the
            // database. Wrapped per-session so one bad row can't stop the rest, and the whole
            // thing can never throw out of the background loop.
            foreach (var session in evicted)
            {
                if (session.ActivityLogFilePath is null ||
                    session.ParticipantId is null ||
                    session.StudySessionId is null)
                {
                    continue;
                }

                try
                {
                    _activityLog.LogEvent(
                        session.ActivityLogFilePath, session.ParticipantId, session.StudySessionId.Value,
                        session.Mode, "SessionExpired", null, DateTime.UtcNow, DateTime.UtcNow);

                    await _study.SaveActivityLogAsync(
                        session.ActivityLogFilePath, session.ParticipantId, session.StudySessionId.Value,
                        session.Mode, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex, "Failed to persist activity log for expired session of participant {ParticipantId}",
                        session.ParticipantId);
                }
                finally
                {
                    // This is the "no one is ever coming back to this session" path — free the
                    // buffer regardless of whether the persist above succeeded.
                    _activityLog.ReleaseLog(session.ActivityLogFilePath);
                }
            }
        }
    }
}
