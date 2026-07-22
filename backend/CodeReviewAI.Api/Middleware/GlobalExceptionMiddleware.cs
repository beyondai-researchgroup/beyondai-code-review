using CodeReviewAI.Api.Exceptions;

namespace CodeReviewAI.Api.Middleware;

/// <summary>
/// Catches any unhandled exception and returns a consistent JSON error envelope.
/// Only writes to the response when headers have not yet been sent (i.e. not mid-stream).
/// </summary>
internal sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    /// <summary>Creates the middleware.</summary>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Executes the pipeline step.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not an application error; swallow silently.
        }
        catch (ClaudeApiException ex)
        {
            // The full upstream error body is logged, but never forwarded to the client.
            _logger.LogError(ex, "Claude API failure for {Method} {Path}", context.Request.Method, context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "AI service unavailable.",
                    detail = "The AI service is currently unavailable. Please try again."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            if (!context.Response.HasStarted)
            {
                // Exception details stay in the log only — ex.Message may contain
                // internal paths or upstream response fragments.
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "An unexpected error occurred."
                });
            }
        }
    }
}
