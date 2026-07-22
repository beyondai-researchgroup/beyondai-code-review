using CodeReviewAI.Api.Services;

namespace CodeReviewAI.Tests;

public class SessionServiceTests
{
    [Fact]
    public async Task CreateSessionAsync_ReturnsSessionWithValidGuid()
    {
        var service = new SessionService();

        var session = await service.CreateSessionAsync();

        Assert.True(Guid.TryParse(session.Id, out _));
        Assert.Null(session.PrContext);
    }

    [Fact]
    public async Task GetSessionAsync_UnknownId_ReturnsNull()
    {
        var service = new SessionService();

        var result = await service.GetSessionAsync(Guid.NewGuid().ToString());

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveExpiredSessions_SessionBeyondMaxAge_IsEvicted()
    {
        var service = new SessionService();
        var session = await service.CreateSessionAsync();

        session.LastActivityAt = DateTime.UtcNow.AddHours(-3);
        await service.UpdateSessionAsync(session);

        service.RemoveExpiredSessions(TimeSpan.FromHours(2));

        var result = await service.GetSessionAsync(session.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveExpiredSessions_ActiveSession_IsRetained()
    {
        var service = new SessionService();
        var session = await service.CreateSessionAsync();

        service.RemoveExpiredSessions(TimeSpan.FromHours(2));

        var result = await service.GetSessionAsync(session.Id);
        Assert.NotNull(result);
    }
}
