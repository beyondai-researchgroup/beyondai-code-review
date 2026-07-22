using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Manages the lifecycle of in-memory review sessions.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new empty session and stores it.
    /// PR data must be loaded separately via the load-pr endpoint.
    /// </summary>
    /// <returns>The newly created <see cref="ReviewSession"/> with a unique ID.</returns>
    Task<ReviewSession> CreateSessionAsync();

    /// <summary>
    /// Returns the session with the given identifier, or <c>null</c> if it does not exist.
    /// Does not throw for unknown IDs.
    /// </summary>
    /// <param name="sessionId">The session ID to look up.</param>
    Task<ReviewSession?> GetSessionAsync(string sessionId);

    /// <summary>
    /// Persists changes made to an existing session — typically a new chat message
    /// or an updated <see cref="ReviewSession.LastActivityAt"/>.
    /// </summary>
    /// <param name="session">The modified session to write back to the store.</param>
    Task UpdateSessionAsync(ReviewSession session);

    /// <summary>Removes the session from the store immediately.</summary>
    /// <param name="sessionId">The session ID to delete.</param>
    Task DeleteSessionAsync(string sessionId);
}
