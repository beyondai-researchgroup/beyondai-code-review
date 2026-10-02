using Amazon.S3;
using Amazon.S3.Model;

namespace CodeReviewAI.Api.Services;

/// <inheritdoc cref="IBlobStorageService" />
internal sealed class R2BlobStorageService : IBlobStorageService
{
    private readonly Lazy<AmazonS3Client?> _client;
    private readonly string? _bucket;

    public R2BlobStorageService(IConfiguration configuration)
    {
        var accountId = configuration["CloudStorage:R2:AccountId"];
        var accessKeyId = configuration["CloudStorage:R2:AccessKeyId"];
        var secretAccessKey = configuration["CloudStorage:R2:SecretAccessKey"];
        _bucket = configuration["CloudStorage:R2:Bucket"];

        _client = new Lazy<AmazonS3Client?>(() =>
        {
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(accessKeyId) ||
                string.IsNullOrWhiteSpace(secretAccessKey) || string.IsNullOrWhiteSpace(_bucket))
            {
                return null;
            }

            return new AmazonS3Client(
                accessKeyId,
                secretAccessKey,
                new AmazonS3Config
                {
                    ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                    AuthenticationRegion = "auto",
                    ForcePathStyle = true,
                });
        });
    }

    /// <inheritdoc />
    public bool IsConfigured => _client.Value is not null;

    /// <inheritdoc />
    public async Task<string> PutTextAsync(string key, string content, string contentType, CancellationToken ct)
    {
        if (_client.Value is null) throw new InvalidOperationException("R2 is not configured");

        await _client.Value.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            ContentBody = content,
            ContentType = contentType,
        }, ct);
        return key;
    }

    /// <inheritdoc />
    public async Task<string> GetTextAsync(string key, CancellationToken ct)
    {
        if (_client.Value is null) throw new InvalidOperationException("R2 is not configured");

        using var response = await _client.Value.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _bucket,
            Key = key,
        }, ct);
        using var reader = new StreamReader(response.ResponseStream);
        return await reader.ReadToEndAsync(ct);
    }
}
