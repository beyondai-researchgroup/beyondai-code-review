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

    /// <summary>Creates the cleanup service.</summary>
    /// <param name="sessions">
    /// The singleton session store. Injected as the concrete type so the
    /// internal <c>RemoveExpiredSessions</c> method is accessible.
    /// </param>
    /// <param name="configuration">Application configuration.</param>
    public SessionCleanupService(SessionService sessions, IConfiguration configuration)
    {
        _sessions = sessions;
        _configuration = configuration;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CleanupInterval, stoppingToken);

            var timeoutMinutes = _configuration.GetValue<int>("Session:TimeoutMinutes", 120);
            _sessions.RemoveExpiredSessions(TimeSpan.FromMinutes(timeoutMinutes));
        }
    }
}
