namespace CodeReviewAI.Api.Services;

/// <summary>
/// Predefined PR short-summary text used when <c>Session:UseStaticSummary</c> is on — every
/// study participant must see identical wording, so this replaces the fresh (non-deterministic)
/// AI paraphrase that would otherwise be generated per session. Toggle the flag off in
/// configuration to restore live AI generation (that code path in
/// <see cref="ReviewSessionEndpoints.FetchPrWithSummaryAsync"/> is left untouched) — same
/// pattern as <see cref="StaticReportContent"/>/<c>Session:UseStaticReport</c>.
/// </summary>
internal static class StaticSummaryContent
{
    /// <summary>Returns the predefined short summary for the given UI language.</summary>
    public static string Get(string lang) => lang == "en" ? En : Sr;

    private const string Sr =
        "Ovaj PR zamenjuje zastareli MD5 algoritam u klasi HashFunctions modernim i bezbednijim " +
        "heš funkcijama — SHA-1, SHA-224, SHA-256, SHA-384 i SHA-512 — usklađenim sa NIST FIPS " +
        "180-4 standardom. Klasa MerkleRoot je ažurirana da interno koristi SHA-256/512 umesto " +
        "MD5, a dodati su i odgovarajući unit testovi koji potvrđuju ispravnost novih algoritama.";

    private const string En =
        "This PR replaces the outdated MD5 algorithm in the HashFunctions class with modern, more " +
        "secure hash functions — SHA-1, SHA-224, SHA-256, SHA-384, and SHA-512 — aligned with the " +
        "NIST FIPS 180-4 standard. The MerkleRoot class was updated to use SHA-256/512 internally " +
        "instead of MD5, and unit tests were added to confirm the new algorithms behave correctly.";
}
