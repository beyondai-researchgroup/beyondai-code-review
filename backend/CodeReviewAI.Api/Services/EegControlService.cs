using System.Net.Http.Json;

namespace CodeReviewAI.Api.Services;

/// <summary>
/// HTTP client for the local EEG control API exposed by the <c>EmotivEEG_DataCollecting</c>
/// console app (loopback only, see its <c>RunControlServerAsync</c>). Disabled by default
/// (<c>Eeg:Enabled</c> is false in base appsettings.json, true only in
/// appsettings.Development.json) so the public Render deployment never attempts to reach
/// a local-only address.
/// </summary>
internal sealed class EegControlService : IEegControlService
{
    private readonly HttpClient _http;
    private readonly bool _enabled;
    private readonly ILogger<EegControlService> _logger;

    public EegControlService(HttpClient http, IConfiguration configuration, ILogger<EegControlService> logger)
    {
        _http = http;
        _logger = logger;
        _enabled = configuration.GetValue<bool>("Eeg:Enabled");
    }

    public Task StartAsync(string participantId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "start", new { participantId }, ct);

    public Task ResumeAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "resume", null, ct);

    public Task PauseAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "pause", null, ct);

    public Task MarkerAsync(string code, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"marker/{Uri.EscapeDataString(code)}", null, ct);

    public Task StopAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "stop", null, ct);

    private async Task SendAsync(HttpMethod method, string route, object? body, CancellationToken ct)
    {
        if (!_enabled)
            return;

        try
        {
            using var request = new HttpRequestMessage(method, route)
            {
                // HttpListener on the EEG app's side (System.Net.HttpListener) requires a
                // Content-Length on POST requests — always attach a body, even an empty
                // object, rather than leaving Content null.
                Content = JsonContent.Create(body ?? new { })
            };

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("EEG control call {Route} returned {Status}", route, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EEG control call {Route} failed — is the EEG app running?", route);
        }
    }
}
