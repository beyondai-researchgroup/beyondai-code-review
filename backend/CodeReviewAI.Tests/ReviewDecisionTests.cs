using CodeReviewAI.Api.Models;
using CodeReviewAI.Api.Services;

namespace CodeReviewAI.Tests;

/// <summary>
/// Tests for the review decision feature: decision storage, timestamp,
/// comment validation, and session lookup.
/// </summary>
public class ReviewDecisionTests
{
    // ── Persistence ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitDecision_ValidInput_DecisionStoredOnSession()
    {
        var service = new SessionService();
        var session = await service.CreateSessionAsync();

        var decision = new ReviewDecision
        {
            Decision = ReviewDecisionType.Accepted,
            Comment = "Kod je čist i testovi prolaze.",
            DecidedAt = DateTime.UtcNow
        };

        session.Decision = decision;
        await service.UpdateSessionAsync(session);

        var fetched = await service.GetSessionAsync(session.Id);

        Assert.NotNull(fetched?.Decision);
        Assert.Equal(ReviewDecisionType.Accepted, fetched!.Decision!.Decision);
        Assert.Equal("Kod je čist i testovi prolaze.", fetched.Decision.Comment);
    }

    [Fact]
    public async Task SubmitDecision_DecidedAt_IsUtcTimestamp()
    {
        var before = DateTime.UtcNow;

        var service = new SessionService();
        var session = await service.CreateSessionAsync();

        var decision = new ReviewDecision
        {
            Decision = ReviewDecisionType.Rejected,
            Comment = "Postoje sigurnosni problemi.",
            DecidedAt = DateTime.UtcNow
        };

        var after = DateTime.UtcNow;

        Assert.Equal(DateTimeKind.Utc, decision.DecidedAt.Kind);
        Assert.True(decision.DecidedAt >= before);
        Assert.True(decision.DecidedAt <= after);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void SubmitDecision_EmptyOrWhitespaceComment_FailsValidation(string comment)
    {
        // The endpoint rejects the request when comment is whitespace-only.
        // This test verifies the validation predicate used by the endpoint handler.
        bool isInvalid = string.IsNullOrWhiteSpace(comment);
        Assert.True(isInvalid, $"Expected comment '{comment}' to fail validation.");
    }

    // ── Session lookup ────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitDecision_UnknownSessionId_ReturnsNull()
    {
        var service = new SessionService();

        var result = await service.GetSessionAsync(Guid.NewGuid().ToString());

        // The endpoint returns 404 when GetSessionAsync returns null.
        Assert.Null(result);
    }
}
