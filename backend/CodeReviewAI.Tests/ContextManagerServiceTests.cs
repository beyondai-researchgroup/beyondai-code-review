using CodeReviewAI.Api.Models;
using CodeReviewAI.Api.Services;

namespace CodeReviewAI.Tests;

public class ContextManagerServiceTests
{
    private static PrContext MakePr(List<PrFile>? files = null) =>
        new(
            Title: "Add login flow",
            Description: "Implements OAuth2 login.",
            Author: "alice",
            BaseBranch: "main",
            HeadBranch: "feature/login",
            Files: files ?? [new PrFile("src/auth.cs", "modified", 10, 2, "@@ -1 +1 @@\n+code")],
            CommitMessages: ["feat: add OAuth2 login", "fix: token refresh"],
            LoadedAt: DateTime.UtcNow);

    private static ChatMessage UserMsg(string text) =>
        new("user", text, DateTime.UtcNow);

    private static ChatMessage AssistantMsg(string text) =>
        new("assistant", text, DateTime.UtcNow);

    private readonly ContextManagerService _svc = new();

    // ── 1. Summary always appears as first message ────────────────────────────

    [Fact]
    public void BuildMessages_SummaryIsFirstMessage_AndRoleIsUser()
    {
        var messages = _svc.BuildMessages(MakePr(), [], "Šta je urađeno?");

        var first = messages[0];
        Assert.Equal("user", first.Role);
        Assert.Contains("Add login flow", first.Content);
        Assert.Contains("alice", first.Content);
        Assert.Contains("feature/login", first.Content);
        Assert.Contains("main", first.Content);
    }

    [Fact]
    public void BuildMessages_SummaryContainsCommitsAndFiles()
    {
        var messages = _svc.BuildMessages(MakePr(), [], "Šta je urađeno?");

        var summary = messages[0].Content;
        Assert.Contains("feat: add OAuth2 login", summary);
        Assert.Contains("src/auth.cs", summary);
        Assert.Contains("+10/-2", summary);
    }

    [Fact]
    public void BuildMessages_SecondMessageIsAssistantAck()
    {
        var messages = _svc.BuildMessages(MakePr(), [], "Šta je urađeno?");

        Assert.Equal("assistant", messages[1].Role);
        Assert.Contains("Razumem", messages[1].Content);
    }

    // ── 2. File patch included when file name appears in the question ─────────

    [Fact]
    public void BuildMessages_FileNameInQuestion_IncludesPatch()
    {
        var pr = MakePr([new PrFile("src/auth.cs", "modified", 5, 1, "@@ -1 +1 @@\n+patch content")]);

        var messages = _svc.BuildMessages(pr, [], "Objasni izmene u src/auth.cs");

        var patchMessage = messages.FirstOrDefault(m => m.Content.Contains("patch content"));
        Assert.NotNull(patchMessage);
        Assert.Equal("user", patchMessage.Role);
    }

    [Fact]
    public void BuildMessages_AnyQuestion_IncludesPatchByDefault()
    {
        // Patches are always included (up to MaxGeneralPatchFiles) so the AI has code to analyse.
        var pr = MakePr([new PrFile("src/auth.cs", "modified", 5, 1, "@@ -1 +1 @@\n+patch content")]);

        var messages = _svc.BuildMessages(pr, [], "Ko je autor?");

        Assert.Contains(messages, m => m.Content.Contains("patch content"));
    }

    [Fact]
    public void BuildMessages_GeneralKeyword_IncludesUpToThreeFiles()
    {
        var files = Enumerable.Range(1, 5)
            .Select(i => new PrFile($"file{i}.cs", "modified", i, 0, $"patch{i}"))
            .ToList();

        var messages = _svc.BuildMessages(MakePr(files), [], "explain everything");

        // Patches from files 1–3 should appear; file 4 and 5 should not.
        var patchBlock = messages.FirstOrDefault(m => m.Content.Contains("patch1"));
        Assert.NotNull(patchBlock);
        Assert.Contains("patch2", patchBlock!.Content);
        Assert.Contains("patch3", patchBlock.Content);
        Assert.DoesNotContain("patch4", patchBlock.Content);
    }

    // ── 3. History is capped at 10 messages ──────────────────────────────────

    [Fact]
    public void BuildMessages_HistoryExceedsCap_OnlyLast10Included()
    {
        var history = Enumerable.Range(1, 15)
            .Select(i => UserMsg($"message {i}"))
            .ToList<ChatMessage>();

        var messages = _svc.BuildMessages(MakePr(), history, "nova pitanja");

        // Only messages 6–15 should appear (last 10).
        Assert.DoesNotContain(messages, m => m.Content == "message 5");
        Assert.Contains(messages, m => m.Content == "message 6");
        Assert.Contains(messages, m => m.Content == "message 15");
    }

    [Fact]
    public void BuildMessages_HistoryAtOrBelowCap_AllIncluded()
    {
        var history = Enumerable.Range(1, 10)
            .Select(i => UserMsg($"message {i}"))
            .ToList<ChatMessage>();

        var messages = _svc.BuildMessages(MakePr(), history, "nova pitanja");

        for (int i = 1; i <= 10; i++)
            Assert.Contains(messages, m => m.Content == $"message {i}");
    }

    // ── 4. Null patch + no full content → fallback note ──────────────────────

    [Fact]
    public void BuildMessages_NullPatch_NullFullContent_FallbackNoteInPatchBlock()
    {
        var pr = MakePr([new PrFile("big-file.cs", "modified", 500, 0, null)]);

        var messages = _svc.BuildMessages(pr, [], "Šta je izmenjeno u big-file.cs?");

        var patchMsg = messages.FirstOrDefault(m => m.Content.Contains("big-file.cs") && m.Role == "user" && messages.IndexOf(m) > 1);
        Assert.NotNull(patchMsg);
        Assert.Contains("Sadržaj fajla nije dostupan", patchMsg!.Content);
    }

    [Fact]
    public void BuildMessages_NullPatch_FullContentPresent_FullContentIncluded()
    {
        var pr = MakePr([new PrFile("big-file.cs", "modified", 500, 0, null, "class BigFile { }")]);

        var messages = _svc.BuildMessages(pr, [], "Šta je izmenjeno u big-file.cs?");

        var patchMsg = messages.FirstOrDefault(m => m.Content.Contains("big-file.cs") && m.Role == "user" && messages.IndexOf(m) > 1);
        Assert.NotNull(patchMsg);
        Assert.Contains("class BigFile", patchMsg!.Content);
        Assert.Contains("Diff nije dostupan", patchMsg.Content);
    }

    [Fact]
    public void BuildMessages_NullPatch_GeneralQuestion_FallbackNoteIncluded()
    {
        var pr = MakePr([new PrFile("big-file.cs", "modified", 500, 0, null)]);

        var messages = _svc.BuildMessages(pr, [], "overview");

        Assert.Contains(messages, m => m.Content.Contains("Sadržaj fajla nije dostupan"));
    }

    // ── 5. Current question is always last ────────────────────────────────────

    [Fact]
    public void BuildMessages_CurrentQuestionIsLastMessage()
    {
        var question = "Koji su potencijalni problemi?";

        var messages = _svc.BuildMessages(MakePr(), [UserMsg("prethodno"), AssistantMsg("odgovor")], question);

        var last = messages[^1];
        Assert.Equal("user", last.Role);
        Assert.Equal(question, last.Content);
    }
}
