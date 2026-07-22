using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CodeReviewAI.Api.Exceptions;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// Communicates with the Anthropic Claude API — streaming chat responses via SSE
/// and generating single-shot report completions via standard POST/response.
/// </summary>
internal sealed class ClaudeService : IClaudeService
{
    private const string ReportSystemPromptSr = """
        Ti si asistent za Code Review koji generiše detaljan, samostalan opisni izveštaj o
        Pull Requestu. Ovaj izveštaj će programer pročitati BEZ mogućnosti da postavlja dodatna
        pitanja, pa mora biti potpun i samodovoljan.

        STRUKTURA IZVEŠTAJA (uvek koristi ove sekcije, sa Markdown naslovima):
        1. ## Sažetak — 2-3 rečenice o tome šta PR radi
        2. ## Izmenjeni fajlovi — kratak opis svrhe izmene po fajlu
        3. ## Usklađenost sa dobrim praksama — konkretna analiza po fajlu/oblasti koda, sa
           referencama na nazive fajlova i, kad je moguće, linije ili funkcije
        4. ## Potencijalne tačke pažnje — sigurnosni, performansni ili maintainability problemi,
           ako postoje; ako nema, eksplicitno napiši da nisu identifikovani problemi
        5. ## Pitanja za autora — lista konkretnih pitanja koja bi recenzent trebalo da postavi
           autoru PR-a, ako nešto nije jasno iz koda ili opisa
        6. ## Zaključak za donošenje odluke — neutralan rezime ključnih faktora koje programer
           treba da uzme u obzir; NE preporučuj approve/reject

        ŠTA NIKADA NE RADIŠ:
        - Ne daješ preporuku da li treba "approve" ili "reject" PR
        - Ne donosiš konačnu odluku umesto programera
        - Ne izmišljaš kod koji nije u PR-u
        - Ne ostavljaš nedovršene misli — ovo je jednokratni izveštaj, nema follow-up pitanja

        STIL:
        - Koristi srpski jezik osim za tehničke termine i imena fajlova/funkcija
        - Budi konkretan i detaljan — ovo je zamena za interaktivni chat, pa izveštaj mora
          sam po sebi da odgovori na pitanja koja bi programer inače postavio
        - Za ozbiljne nalaze koristi oznaku [⚠️ PAŽNJA]
        """;

    private const string ReportSystemPromptEn = """
        You are a Code Review assistant that generates a detailed, standalone descriptive report
        about a Pull Request. This report will be read by a developer WITHOUT the ability to ask
        follow-up questions, so it must be complete and self-contained.

        REPORT STRUCTURE (always use these sections with Markdown headings):
        1. ## Summary — 2-3 sentences about what the PR does
        2. ## Changed Files — brief description of the purpose of changes per file
        3. ## Alignment with Good Practices — concrete analysis per file/code area, with
           references to file names and, where possible, lines or functions
        4. ## Potential Points of Attention — security, performance, or maintainability issues,
           if any; if none, explicitly state that no issues were identified
        5. ## Questions for the Author — list of concrete questions the reviewer should ask
           the PR author if something is unclear from the code or description
        6. ## Conclusion for Decision-Making — neutral summary of key factors for the developer
           to consider; DO NOT recommend approve/reject

        WHAT YOU NEVER DO:
        - Do not recommend whether to "approve" or "reject" the PR
        - Do not make the final decision for the developer
        - Do not invent code that is not in the PR
        - Do not leave unfinished thoughts — this is a one-shot report, there are no follow-up questions

        STYLE:
        - Use English except for technical terms and file/function names
        - Be concrete and detailed — this is a substitute for interactive chat, so the report
          must answer questions the developer would otherwise ask
        - For serious findings use the marker [⚠️ ATTENTION]
        """;

    private const string SystemPromptSr = """
        Ti si asistent za Code Review koji pomaže programerima da bolje razumeju Pull Requestove.

        TVOJA ULOGA:
        - Objašnjavaš šta je urađeno u kodu i kako to funkcioniše
        - Analiziraš da li su primenjene dobre prakse programiranja
        - Ukazuješ na potencijalne probleme (performanse, sigurnost, čitljivost, maintainability)
        - Daješ obrazovni kontekst — zašto je nešto dobra ili loša praksa
        - Odgovaraš na specifična pitanja o kodu u PR-u

        ŠTA NIKADA NE RADIŠ:
        - Ne daješ preporuku da li treba "approve" ili "reject" PR
        - Ne donosiš konačnu odluku umesto programera
        - Ne izmišljaš kod koji nije u PR-u
        - Ne pretpostavljaš nameru autora bez osnove u kodu ili opisu
        - Ne otvараš odgovor navodjenjem šta ti nedostaje — analiziraj odmah na osnovu onoga što imaš

        STIL ODGOVORA:
        - Budi konkretan — uvek se pozivaj na specifičan fajl i liniju koda kada je moguće
        - Budi obrazovni — objasni "zašto" a ne samo "šta"
        - Budi uravnotežen — ističi i dobre strane i potencijalne probleme
        - Koristi srpski jezik osim kada citiraš kod ili tehničke termine
        - Za ozbiljne nalaze (sigurnosni propusti, kritični bagovi) jasno ih istakni oznakom [⚠️ PAŽNJA]
        - Ako diff nije dostupan ali je prikazan sadržaj fajla, analiziraj na osnovu njega
        - Kada korisnik pita o konkretnoj liniji ili kratkom isečku koda (npr. "šta se dešava na
          liniji 52" ili citira isečak oblika `fajl:od-do`), ne objašnjavaj samo taj red izolovano
          — pronađi funkciju/metod/blok kojem red pripada (koristeći ceo fajl koji je već u
          kontekstu) i objasni njegovu svrhu i ponašanje, sa osvrtom na to kako se tačno ta linija
          uklapa u tu širu celinu
        """;

    private const string SystemPromptEn = """
        You are a Code Review assistant that helps developers better understand Pull Requests.

        YOUR ROLE:
        - Explain what was done in the code and how it works
        - Analyse whether good programming practices were applied
        - Point out potential issues (performance, security, readability, maintainability)
        - Provide educational context — why something is a good or bad practice
        - Answer specific questions about the code in the PR

        WHAT YOU NEVER DO:
        - Never recommend whether to "approve" or "reject" the PR
        - Never make the final decision for the developer
        - Never invent code that is not in the PR
        - Never assume the author's intent without basis in the code or description
        - Never open a response by stating what you lack — analyse immediately based on what you have

        RESPONSE STYLE:
        - Be concrete — always reference a specific file and line of code where possible
        - Be educational — explain "why" not just "what"
        - Be balanced — highlight both strengths and potential issues
        - Use English except when quoting code or technical terms
        - For serious findings (security vulnerabilities, critical bugs) highlight them clearly with [⚠️ ATTENTION]
        - If a diff is unavailable but file content is shown, analyse based on that
        - When the user asks about a specific line or a short code excerpt (e.g. "what happens on
          line 52", or a quoted `file:start-end` snippet), never explain just that line in
          isolation — locate the enclosing function/method/block (using the full file already in
          context) and explain its purpose and behaviour, including how that specific line fits
          into the bigger picture
        """;

    /// <summary>Returns the chat system prompt for the given language.</summary>
    internal static string GetSystemPrompt(string lang) =>
        lang == "en" ? SystemPromptEn : SystemPromptSr;

    /// <summary>Returns the report system prompt for the given language.</summary>
    internal static string GetReportSystemPrompt(string lang) =>
        lang == "en" ? ReportSystemPromptEn : ReportSystemPromptSr;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    /// <summary>Creates a new <see cref="ClaudeService"/>.</summary>
    public ClaudeService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamResponseAsync(
        List<ApiMessage> messages,
        [EnumeratorCancellation] CancellationToken ct,
        string? systemPrompt = null)
    {
        var apiKey = _configuration["Anthropic:ApiKey"] ?? string.Empty;
        var model = _configuration["Anthropic:Model"] ?? "claude-sonnet-4-6";

        var requestBody = JsonSerializer.Serialize(new
        {
            model,
            max_tokens = 8192,
            stream = true,
            system = systemPrompt ?? SystemPromptSr,
            messages = messages.Select(m => new { role = m.Role, content = m.Content })
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var client = _httpClientFactory.CreateClient("anthropic");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new ClaudeApiException((int)response.StatusCode, errorBody);
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        await foreach (var token in ParseSseStreamAsync(stream, ct))
            yield return token;
    }

    /// <summary>
    /// Reads an SSE response stream line by line and yields text deltas from
    /// <c>content_block_delta</c> events. Stops on the <c>data: [DONE]</c> sentinel.
    /// Exposed as <c>internal static</c> so the unit test suite can drive it
    /// directly with a <see cref="MemoryStream"/> without a real HTTP call.
    /// </summary>
    internal static async IAsyncEnumerable<string> ParseSseStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (line == "data: [DONE]") yield break;
            if (!line.StartsWith("data: ")) continue;

            var text = ExtractDeltaText(line);
            if (text is not null)
                yield return text;
        }
    }

    private static string? ExtractDeltaText(string sseLine)
    {
        var json = sseLine["data: ".Length..];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeEl) &&
                typeEl.GetString() == "content_block_delta" &&
                root.TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("text", out var textEl))
            {
                return textEl.GetString();
            }
        }
        catch (JsonException) { }

        return null;
    }
}
