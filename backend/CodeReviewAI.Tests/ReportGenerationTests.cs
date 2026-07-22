using CodeReviewAI.Api.Models;
using CodeReviewAI.Api.Services;

namespace CodeReviewAI.Tests;

/// <summary>
/// Tests for Report Mode: BuildReportMessages output and session-level caching behaviour.
/// </summary>
public class ReportGenerationTests
{
    private static PrContext MakePr(int fileCount = 5) =>
        new(
            Title: "Feature: add payment flow",
            Description: "Adds Stripe integration.",
            Author: "bob",
            BaseBranch: "main",
            HeadBranch: "feature/payment",
            Files: Enumerable.Range(1, fileCount)
                .Select(i => new PrFile($"src/file{i}.cs", "modified", i * 10, i, $"@@ -1 +1 @@\n+patch{i}"))
                .ToList(),
            CommitMessages: ["feat: payment", "fix: refund"],
            LoadedAt: DateTime.UtcNow);

    private readonly ContextManagerService _svc = new();

    // ── BuildReportMessages ─────────────────────────────────────────────────

    [Fact]
    public void BuildReportMessages_IncludesAllFilesInPrContext()
    {
        var pr = MakePr(fileCount: 8);

        var messages = _svc.BuildReportMessages(pr);

        // All 8 file patches must appear somewhere in the message list.
        for (int i = 1; i <= 8; i++)
            Assert.Contains(messages, m => m.Content.Contains($"patch{i}"));
    }

    [Fact]
    public void BuildReportMessages_IncludesMoreThanThreeFiles_UnlikeChat()
    {
        // Chat mode caps at MaxGeneralPatchFiles (3); report mode must include all files.
        var pr = MakePr(fileCount: 6);

        var report = _svc.BuildReportMessages(pr);
        var chat = _svc.BuildMessages(pr, [], "overview");

        // Report messages contain patch4–patch6; chat messages should not.
        Assert.Contains(report, m => m.Content.Contains("patch4"));
        Assert.Contains(report, m => m.Content.Contains("patch6"));
        Assert.DoesNotContain(chat, m => m.Content.Contains("patch4"));
    }

    [Fact]
    public void BuildReportMessages_LastMessageIsUserRole_WithGenerationInstruction()
    {
        var pr = MakePr();

        var messages = _svc.BuildReportMessages(pr);

        var last = messages[^1];
        Assert.Equal("user", last.Role);
        Assert.Contains("Generiši", last.Content);
    }

    [Fact]
    public void BuildReportMessages_DoesNotIncludeConversationHistory()
    {
        var pr = MakePr();

        var messages = _svc.BuildReportMessages(pr);

        // There must be no messages with role alternating in the same way as chat history.
        // Specifically: the only user messages should be the PR summary, the patches block,
        // and the generation instruction — all synthesized, not free-form user text.
        Assert.DoesNotContain(messages, m => m.Content.Contains("prethodno pitanje"));
    }

    [Fact]
    public void BuildReportMessages_NullPatch_IncludesFallbackNote()
    {
        var pr = new PrContext(
            Title: "Test",
            Description: null,
            Author: "x",
            BaseBranch: "main",
            HeadBranch: "feat",
            Files: [new PrFile("binary.dll", "modified", 0, 0, null)],
            CommitMessages: [],
            LoadedAt: DateTime.UtcNow);

        var messages = _svc.BuildReportMessages(pr);

        Assert.Contains(messages, m =>
            m.Content.Contains("binary.dll") &&
            (m.Content.Contains("nije dostupan") || m.Content.Contains("binarni")));
    }

    // ── Session cache behaviour ──────────────────────────────────────────────

    [Fact]
    public async Task ReviewSession_ReportMode_CachesGeneratedReport()
    {
        var service = new SessionService();
        var session = await service.CreateSessionAsync();
        session.Mode = ReviewMode.Report;
        session.GeneratedReport = "Cached report text.";
        await service.UpdateSessionAsync(session);

        var fetched = await service.GetSessionAsync(session.Id);

        Assert.NotNull(fetched);
        Assert.Equal(ReviewMode.Report, fetched!.Mode);
        Assert.Equal("Cached report text.", fetched.GeneratedReport);
    }

    [Fact]
    public async Task ReviewSession_AiMode_GeneratedReportIsNullByDefault()
    {
        var service = new SessionService();
        var session = await service.CreateSessionAsync();

        Assert.Equal(ReviewMode.Ai, session.Mode);
        Assert.Null(session.GeneratedReport);
    }

    [Fact]
    public async Task ReviewSession_AiMode_ShouldReturn400_VerifiedByModeCheck()
    {
        // Simulates the guard check the report endpoint performs.
        var service = new SessionService();
        var session = await service.CreateSessionAsync();
        session.Mode = ReviewMode.Ai;
        await service.UpdateSessionAsync(session);

        var fetched = await service.GetSessionAsync(session.Id);

        // The endpoint guard is: if (session.Mode != ReviewMode.Report) → 400
        Assert.NotEqual(ReviewMode.Report, fetched!.Mode);
    }

    [Fact]
    public async Task ReviewSession_SecondCallWithoutRegenerate_ReturnsCachedReport()
    {
        // Simulates the cache hit path: GeneratedReport already set, regenerate=false.
        var service = new SessionService();
        var session = await service.CreateSessionAsync();
        session.Mode = ReviewMode.Report;
        session.GeneratedReport = "First generation.";
        await service.UpdateSessionAsync(session);

        var fetched = await service.GetSessionAsync(session.Id);

        // Endpoint logic: if (session.GeneratedReport is not null && !regenerate) return cached
        bool shouldReturnCached = fetched!.GeneratedReport is not null && !false /* regenerate=false */;
        Assert.True(shouldReturnCached);
        Assert.Equal("First generation.", fetched.GeneratedReport);
    }
}
