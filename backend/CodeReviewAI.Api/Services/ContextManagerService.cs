using System.Text;
using CodeReviewAI.Api.Models;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Builds the Claude API message list for each review conversation turn.
/// The sequence is always: PR summary → assistant ack → relevant patches (if any)
/// → patch ack (if patches were added) → last 10 history messages → current question.
/// </summary>
internal sealed class ContextManagerService : IContextManagerService
{
    private const int MaxHistoryMessages = 10;
    private const int MaxGeneralPatchFiles = 3;

    /// <summary>
    /// Words that, when present in the user's question, indicate they're asking about a
    /// referenced standard/specification — gates whether <see cref="BuildDocsBlock"/> pulls
    /// the (otherwise invisible) Docs-folder content into the prompt. Deliberately narrow: the
    /// PR's own subject matter (e.g. hashing) would match far too often on broader terms.
    /// </summary>
    private static readonly string[] DocsTriggerWords =
    [
        "standard", "specifikacij", "specification", "spec", "propis", "fips", "nist"
    ];

    /// <inheritdoc />
    public List<ApiMessage> BuildMessages(PrContext pr, List<ChatMessage> history, string userQuestion, string? repoContext = null, string? docsContent = null, string lang = "sr")
    {
        var messages = new List<ApiMessage>();

        // 1. Repository context — optional, injected before the PR summary.
        if (repoContext is not null)
        {
            messages.Add(new ApiMessage("user", repoContext));
            messages.Add(new ApiMessage("assistant", L.RepoContextAck(lang)));
        }

        // 2. PR summary — always present.
        messages.Add(new ApiMessage("user", BuildPrSummary(pr, lang)));

        // 3. Assistant acknowledgement of the PR summary.
        messages.Add(new ApiMessage("assistant", L.PrSummaryAck(lang)));

        // 4. Relevant file patches — conditional.
        var patches = BuildPatchBlock(pr, userQuestion, lang);
        if (patches is not null)
        {
            messages.Add(new ApiMessage("user", patches));
            messages.Add(new ApiMessage("assistant", L.PatchAck(lang)));
        }

        // 4b. Reference standard/specification — conditional, only when the question itself
        // seems to be asking about one. This content is never shown anywhere in the UI.
        var docsBlock = BuildDocsBlock(docsContent, userQuestion, lang);
        if (docsBlock is not null)
        {
            messages.Add(new ApiMessage("user", docsBlock));
            messages.Add(new ApiMessage("assistant", L.DocsAck(lang)));
        }

        // 5. Conversation history — last N messages only.
        var tail = history.Count > MaxHistoryMessages
            ? history[^MaxHistoryMessages..]
            : history;

        foreach (var msg in tail)
            messages.Add(new ApiMessage(msg.Role, msg.Content));

        // 6. Current user question.
        messages.Add(new ApiMessage("user", userQuestion));

        return messages;
    }

    private static string BuildPrSummary(PrContext pr, string lang)
    {
        var sb = new StringBuilder();
        sb.AppendLine(lang == "en" ? "## Pull Request overview" : "## Pull Request pregled");
        sb.AppendLine();
        sb.AppendLine($"{L.TitleLabel(lang)} {pr.Title}");
        sb.AppendLine($"{L.AuthorLabel(lang)} {pr.Author}");
        sb.AppendLine($"{L.BranchesLabel(lang)} `{pr.HeadBranch}` → `{pr.BaseBranch}`");

        if (!string.IsNullOrWhiteSpace(pr.Description))
        {
            sb.AppendLine();
            sb.AppendLine($"{L.DescriptionLabel(lang)}");
            sb.AppendLine(pr.Description);
        }

        if (pr.CommitMessages.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{L.CommitsLabel(lang)}");
            foreach (var commit in pr.CommitMessages)
                sb.AppendLine($"- {commit}");
        }

        if (pr.Files.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{L.FilesLabel(lang)}");
            foreach (var file in pr.Files)
                sb.AppendLine($"- `{file.FileName}` [{file.Status}] +{file.Additions}/-{file.Deletions}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <inheritdoc />
    public List<ApiMessage> BuildReportMessages(PrContext pr, string lang = "sr")
    {
        var messages = new List<ApiMessage>();

        // 1. Full PR summary (same helper as chat mode).
        messages.Add(new ApiMessage("user", BuildPrSummary(pr, lang)));
        messages.Add(new ApiMessage("assistant", L.ReportPrSummaryAck(lang)));

        // 2. ALL file patches — no heuristic trimming.
        if (pr.Files.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine(lang == "en" ? "## All code changes" : "## Sve izmene koda");
            sb.AppendLine();

            foreach (var file in pr.Files)
            {
                sb.AppendLine($"### {file.FileName}");

                if (file.Patch is not null)
                {
                    sb.AppendLine("```diff");
                    sb.AppendLine(file.Patch);
                    sb.AppendLine("```");
                }
                else if (file.FullContent is not null)
                {
                    sb.AppendLine(L.DiffUnavailableShowingContent(lang));
                    sb.AppendLine("```");
                    sb.AppendLine(file.FullContent);
                    sb.AppendLine("```");
                }
                else
                {
                    sb.AppendLine(L.ContentUnavailable(lang));
                }

                sb.AppendLine();
            }

            messages.Add(new ApiMessage("user", sb.ToString().TrimEnd()));
            messages.Add(new ApiMessage("assistant", L.AllPatchesAck(lang)));
        }

        // 3. Fixed generation instruction.
        messages.Add(new ApiMessage("user", L.GenerateReportInstruction(lang)));

        return messages;
    }

    /// <inheritdoc />
    public List<ApiMessage> BuildSuggestionsMessages(PrContext pr, List<ChatMessage> history, string lang = "sr")
    {
        var messages = new List<ApiMessage>();

        messages.Add(new ApiMessage("user", BuildPrSummary(pr, lang)));
        messages.Add(new ApiMessage("assistant", L.PrSummaryAck(lang)));

        // Include the last few turns for contextual relevance, but keep it lightweight.
        var tail = history.Count > 6 ? history[^6..] : history;
        foreach (var msg in tail)
            messages.Add(new ApiMessage(msg.Role, msg.Content));

        var askedQuestions = history
            .Where(m => m.Role == "user")
            .Select((m, i) => $"{i + 1}. {m.Content}")
            .ToList();

        var exclusionBlock = askedQuestions.Count > 0
            ? $"\n\n{L.SuggestionsExclusionPrefix(lang)}\n{string.Join('\n', askedQuestions)}"
            : string.Empty;

        messages.Add(new ApiMessage("user", L.SuggestionsInstruction(lang, exclusionBlock)));

        return messages;
    }

    /// <inheritdoc />
    public List<ApiMessage>? BuildShortSummaryMessages(string? description, string lang = "sr")
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        return
        [
            new ApiMessage("user", L.ShortSummaryInstruction(lang, description))
        ];
    }

    private static string? BuildPatchBlock(PrContext pr, string userQuestion, string lang)
    {
        var lowerQuestion = userQuestion.ToLowerInvariant();

        // Files explicitly mentioned in the question take priority.
        // Otherwise include the first N files by default so the AI always has code to work with.
        var mentionedFiles = pr.Files
            .Where(f => lowerQuestion.Contains(f.FileName.ToLowerInvariant()))
            .ToList();

        var filesToInclude = mentionedFiles.Count > 0
            ? mentionedFiles
            : pr.Files.Take(MaxGeneralPatchFiles).ToList();

        if (filesToInclude.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine(lang == "en" ? "## Relevant code changes" : "## Relevantne izmene koda");
        sb.AppendLine();

        foreach (var file in filesToInclude)
        {
            sb.AppendLine($"### {file.FileName}");

            if (file.Patch is not null)
            {
                sb.AppendLine("```diff");
                sb.AppendLine(file.Patch);
                sb.AppendLine("```");
            }
            else if (file.FullContent is not null)
            {
                sb.AppendLine(L.DiffUnavailableShowingContent(lang));
                sb.AppendLine("```");
                sb.AppendLine(file.FullContent);
                sb.AppendLine("```");
            }
            else
            {
                sb.AppendLine(L.ContentUnavailable(lang));
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Surfaces the (otherwise hidden) Docs-folder reference material only when the current
    /// question looks like it's asking about a standard/specification — keeps it out of every
    /// other turn's token budget, matching how <see cref="BuildPatchBlock"/> gates patches.
    /// </summary>
    private static string? BuildDocsBlock(string? docsContent, string userQuestion, string lang)
    {
        if (string.IsNullOrWhiteSpace(docsContent))
            return null;

        var lowerQuestion = userQuestion.ToLowerInvariant();
        if (!DocsTriggerWords.Any(w => lowerQuestion.Contains(w)))
            return null;

        var sb = new StringBuilder();
        sb.AppendLine(lang == "en" ? "## Referenced standard/specification" : "## Referentni standard/specifikacija");
        sb.AppendLine();
        sb.AppendLine(docsContent);
        return sb.ToString().TrimEnd();
    }

    /// <summary>Language-specific string helpers.</summary>
    private static class L
    {
        public static string DocsAck(string lang) => lang == "en"
            ? "Understood. I have reviewed the referenced standard/specification."
            : "Razumem. Pregledao sam referentni standard/specifikaciju.";

        public static string RepoContextAck(string lang) => lang == "en"
            ? "Understood. I have studied the repository structure and key files and am ready to answer questions about the project."
            : "Razumem. Proučio sam strukturu i ključne fajlove repozitorijuma i spreman sam da odgovaram na pitanja o projektu.";

        public static string PrSummaryAck(string lang) => lang == "en"
            ? "Understood. I have loaded the Pull Request details and am ready to answer questions."
            : "Razumem. Učitao sam detalje ovog Pull Requesta i spreman sam da odgovorim na pitanja.";

        public static string PatchAck(string lang) => lang == "en"
            ? "I have reviewed the relevant code changes."
            : "Pregledao sam relevantne izmene koda.";

        public static string ReportPrSummaryAck(string lang) => lang == "en"
            ? "Understood. I have loaded all details of this Pull Request and am ready to generate the report."
            : "Razumem. Učitao sam sve detalje ovog Pull Requesta i spreman sam da generišem izveštaj.";

        public static string AllPatchesAck(string lang) => lang == "en"
            ? "I have reviewed all code changes in this PR."
            : "Pregledao sam sve izmene koda u ovom PR-u.";

        public static string DiffUnavailableShowingContent(string lang) => lang == "en"
            ? "*[Diff unavailable — showing current file content]*"
            : "*[Diff nije dostupan — prikazan je trenutni sadržaj fajla]*";

        public static string ContentUnavailable(string lang) => lang == "en"
            ? "*[File content unavailable — binary file or no access]*"
            : "*[Sadržaj fajla nije dostupan — binarni fajl ili nedostatak pristupa]*";

        public static string TitleLabel(string lang) => lang == "en" ? "**Title:**" : "**Naslov:**";
        public static string AuthorLabel(string lang) => lang == "en" ? "**Author:**" : "**Autor:**";
        public static string BranchesLabel(string lang) => lang == "en" ? "**Branches:**" : "**Grane:**";
        public static string DescriptionLabel(string lang) => lang == "en" ? "**Description:**" : "**Opis:**";
        public static string CommitsLabel(string lang) => lang == "en" ? "**Commit messages:**" : "**Commit poruke:**";
        public static string FilesLabel(string lang) => lang == "en" ? "**Changed files:**" : "**Izmenjeni fajlovi:**";

        public static string GenerateReportInstruction(string lang) => lang == "en"
            ? "Generate a complete report for this Pull Request following the structure and guidelines from the system prompt."
            : "Generiši kompletan izveštaj o ovom Pull Requestu prateći strukturu i smernice iz sistemskog prompta.";

        public static string SuggestionsExclusionPrefix(string lang) => lang == "en"
            ? "Questions already asked — do NOT suggest the same or semantically similar ones:"
            : "Pitanja koja su već postavljana — NE predlaži ista ili semantički slična:";

        public static string SuggestionsInstruction(string lang, string exclusionBlock) => lang == "en"
            ? $"Based on this PR and the conversation so far, suggest exactly 4 short and specific questions that the developer might want to ask next.{exclusionBlock}\n\n" +
              "Return ONLY a JSON array of exactly 4 strings in English, with no additional text, headers, or explanations.\n" +
              "Example: [\"Question 1?\", \"Question 2?\", \"Question 3?\", \"Question 4?\"]"
            : $"Na osnovu ovog PR-a i dosadašnjeg razgovora, predloži tačno 4 kratka i konkretna pitanja koja bi programer možda hteo da postavi sledeće.{exclusionBlock}\n\n" +
              "Vrati ISKLJUČIVO JSON niz od 4 stringa na srpskom jeziku (ekavica), bez ikakvog dodatnog teksta, zaglavlja ili objašnjenja.\n" +
              "Primer: [\"Pitanje 1?\", \"Pitanje 2?\", \"Pitanje 3?\", \"Pitanje 4?\"]";

        public static string ShortSummaryInstruction(string lang, string description) => lang == "en"
            ? "Write a short 2-3 sentence summary of the Pull Request based on the description below.\n\n" +
              "Strict rules:\n" +
              "- Write ONLY in plain text — no markdown formatting whatsoever (no **, *, #, >, ``, or similar characters)\n" +
              "- Write in English\n" +
              "- The summary must be concise: what was done and why, nothing more\n" +
              "- Return ONLY the summary text, with no introduction or conclusion\n\n" +
              $"PR description:\n{description}"
            : "Napiši kratak sažetak Pull Requesta od 2-3 rečenice na osnovu opisa ispod.\n\n" +
              "Stroga pravila:\n" +
              "- Piši ISKLJUČIVO u plain tekstu — bez ikakvog markdown formatiranja (bez **, *, #, >, ``, ili sličnih znakova)\n" +
              "- Piši na srpskom jeziku, ekavica (ne ijekavica)\n" +
              "- Sažetak mora biti koncizan: šta je urađeno i zašto, ništa više\n" +
              "- Vrati SAMO tekst sažetka, bez ikakvog uvoda ili zaključka\n\n" +
              $"Opis PR-a:\n{description}";
    }
}
