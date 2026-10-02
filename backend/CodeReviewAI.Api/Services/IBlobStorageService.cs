namespace CodeReviewAI.Api.Services;

/// <summary>
/// Cloud object storage for files this backend persists outside the study database — today, the
/// Activity Log's final CSV (a real research instrument: see <see cref="StudyService.SaveActivityLogAsync"/>).
/// Backed by Cloudflare R2 (an S3-compatible API, see <see cref="R2BlobStorageService"/>), gated
/// entirely on the <c>CloudStorage:R2:*</c> configuration keys — see
/// admin-dashboard-andrejkatin/docs/cloudflare-r2-setup.md for how to obtain them. When
/// unconfigured, <see cref="IsConfigured"/> is <c>false</c> and callers fall back to storing the
/// content directly in Postgres (<c>ActivityLog.RawCsv</c>), exactly as before this service existed
/// — same "absent config disables the feature, falls back to the pre-existing behavior" discipline
/// as every other optional integration in this codebase.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>True once every required R2 credential is present.</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads UTF-8 text under the given key, returning that same key.</summary>
    Task<string> PutTextAsync(string key, string content, string contentType, CancellationToken ct);

    /// <summary>Downloads an object as UTF-8 text.</summary>
    Task<string> GetTextAsync(string key, CancellationToken ct);

    /// <summary>Builds a namespaced object key, same convention as the Node apps' r2.mjs buildKey().</summary>
    static string BuildKey(string prefix, string scopeId, string originalFilename)
    {
        var safeName = new string(originalFilename.Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-').ToArray());
        if (safeName.Length == 0) safeName = "file";
        if (safeName.Length > 150) safeName = safeName[^150..];
        return $"{prefix}/{scopeId}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{safeName}";
    }
}
