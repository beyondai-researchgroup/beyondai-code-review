using CodeReviewAI.Api.Exceptions;
using CodeReviewAI.Api.Services.GitHub;
using Moq;

namespace CodeReviewAI.Tests;

public class GitHubServiceTests
{
    private static GitHubService CreateService(IGitHubApiAdapter adapter) => new(adapter);

    [Fact]
    public async Task LoadPrContextAsync_OversizedPatch_NullifiesPatchKeepsFileName()
    {
        var largePatch = new string('x', 15_001);
        var adapter = new Mock<IGitHubApiAdapter>();
        adapter
            .Setup(a => a.GetPrDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubPrData(
                "Add feature", null, "alice", "main", "feature/x",
                [new GitHubFileData("large-file.cs", "modified", 200, 10, largePatch)],
                ["chore: initial commit"]));

        var context = await CreateService(adapter.Object)
            .LoadPrContextAsync("tok", "owner", "repo", 1, CancellationToken.None);

        Assert.Single(context.Files);
        Assert.Null(context.Files[0].Patch);
        Assert.Equal("large-file.cs", context.Files[0].FileName);
    }

    [Fact]
    public async Task LoadPrContextAsync_ExactlyAtLimit_RetainsPatch()
    {
        var borderlinePatch = new string('x', 15_000);
        var adapter = new Mock<IGitHubApiAdapter>();
        adapter
            .Setup(a => a.GetPrDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubPrData(
                "Fix bug", null, "bob", "main", "fix/y",
                [new GitHubFileData("file.cs", "modified", 5, 5, borderlinePatch)],
                []));

        var context = await CreateService(adapter.Object)
            .LoadPrContextAsync("tok", "owner", "repo", 2, CancellationToken.None);

        Assert.Equal(borderlinePatch, context.Files[0].Patch);
        Assert.Equal("file.cs", context.Files[0].FileName);
    }

    [Fact]
    public async Task LoadPrContextAsync_MissingPr_PropagatesGitHubIntegrationException()
    {
        var adapter = new Mock<IGitHubApiAdapter>();
        adapter
            .Setup(a => a.GetPrDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GitHubIntegrationException("Pull request #999 was not found in owner/repo."));

        var ex = await Assert.ThrowsAsync<GitHubIntegrationException>(
            () => CreateService(adapter.Object)
                .LoadPrContextAsync("tok", "owner", "repo", 999, CancellationToken.None));

        Assert.Contains("999", ex.Message);
    }
}
