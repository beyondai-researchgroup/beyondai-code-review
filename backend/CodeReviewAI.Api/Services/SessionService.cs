using System.Collections.Concurrent;
using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="ISessionService"/>.
/// </summary>
internal sealed class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, ReviewSession> _sessions = new();

    /// <inheritdoc />
    public Task<ReviewSession> CreateSessionAsync()
    {
        var session = new ReviewSession();
        _sessions[session.Id] = session;
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task<ReviewSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task UpdateSessionAsync(ReviewSession session)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes sessions whose <see cref="ReviewSession.LastActivityAt"/> is older than
    /// <paramref name="maxAge"/>. Called by <see cref="SessionCleanupService"/>.
    /// </summary>
    internal void RemoveExpiredSessions(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var (id, session) in _sessions)
        {
            if (session.LastActivityAt < cutoff)
                _sessions.TryRemove(id, out _);
        }
    }
}
