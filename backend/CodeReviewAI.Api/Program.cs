using CodeReviewAI.Api.Endpoints;
using CodeReviewAI.Api.Middleware;
using CodeReviewAI.Api.Services;
using CodeReviewAI.Api.Services.GitHub;
using Microsoft.Extensions.Configuration.Json;

var builder = WebApplication.CreateBuilder(args);

// Render (and most non-IIS hosts) inject the port to listen on via $PORT rather than
// ASPNETCORE_URLS. Local dev is unaffected — launchSettings.json profiles set their own
// URLs and this variable is never present outside a hosting platform.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Disable appsettings.json hot-reload watchers. The default host wires up a
// FileSystemWatcher (inotify) per JSON config file; constrained containers (e.g. Render's
// free tier) can have a very low per-container inotify instance limit, which crashes the
// app on startup with "The configured user limit (128) on the number of inotify instances
// has been reached". Hot-reload isn't needed anyway — config changes require a redeploy.
foreach (var source in builder.Configuration.Sources.OfType<JsonConfigurationSource>())
    source.ReloadOnChange = false;

builder.Services.AddCors(options =>
{
    // Loopback origins (any localhost/127.0.0.1 port) are always allowed for local dev.
    // Production frontend origins (Vercel URLs) are added via Cors:AllowedOrigins config.
    var allowedOrigins = new HashSet<string>(
        builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [],
        StringComparer.OrdinalIgnoreCase);

    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                  Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                  (uri.IsLoopback || allowedOrigins.Contains(origin)))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks();

builder.Services.AddHttpClient("anthropic", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
});

builder.Services.AddScoped<IClaudeService, ClaudeService>();

builder.Services.AddScoped<IGitHubApiAdapter, OctokitApiAdapter>();
builder.Services.AddScoped<IRemoteRepositoryService, GitHubService>();

builder.Services.AddSingleton<IContextManagerService, ContextManagerService>();

builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<ISessionService>(sp => sp.GetRequiredService<SessionService>());
builder.Services.AddHostedService<SessionCleanupService>();

builder.Services.AddSingleton<IStudyService, StudyService>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors();

app.MapHealthChecks("/health");
app.MapReviewSessionEndpoints();
app.MapStudyEndpoints();

app.Run();
