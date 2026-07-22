namespace CodeReviewAI.Api.Models;

/// <summary>
/// All data fetched from GitHub for a single Pull Request, used as the
/// source of truth when constructing the AI review prompt.
/// </summary>
/// <param name="Title">Title of the Pull Request.</param>
/// <param name="Description">Body text of the Pull Request, or <c>null</c> if the author left it blank.</param>
/// <param name="Author">GitHub login of the PR author.</param>
/// <param name="BaseBranch">Name of the branch the PR targets (e.g. <c>main</c>).</param>
/// <param name="HeadBranch">Name of the branch being merged (the feature branch).</param>
/// <param name="Files">Every file changed by the PR, including diff patches where available.</param>
/// <param name="CommitMessages">Ordered list of commit messages included in the PR.</param>
/// <param name="LoadedAt">UTC timestamp when this context was fetched from GitHub.</param>
public record PrContext(
    string Title,
    string? Description,
    string Author,
    string BaseBranch,
    string HeadBranch,
    List<PrFile> Files,
    List<string> CommitMessages,
    DateTime LoadedAt);
