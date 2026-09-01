using System.Diagnostics;

namespace PMS.Api.Middleware;

/// <summary>
/// Records how long each API request took. This is the operational-log channel, which
/// <b>never contains PHI</b> (planning-pms-verification.md, section 7 "Logging &amp; audit"):
/// method, path template, status and elapsed milliseconds only - no query string, no body,
/// no patient identifiers. Clinical auditing is a separate concern (F-17, AuditEvent).
/// </summary>
public sealed class RequestTimingMiddleware
{
    /// <summary>Requests slower than this are logged at Warning, feeding the F-19 budget work.</summary>
    public const int SlowRequestThresholdMs = 2_000;

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Path only, never PathBase + QueryString: a query string can carry a patient id
            // or a search term that is itself a patient name.
            var path = context.Request.Path.Value ?? "/";

            if (elapsedMs >= SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "{Method} {Path} responded {StatusCode} in {ElapsedMs} ms (over the {ThresholdMs} ms budget).",
                    context.Request.Method,
                    path,
                    context.Response.StatusCode,
                    elapsedMs,
                    SlowRequestThresholdMs);
            }
            else
            {
                _logger.LogInformation(
                    "{Method} {Path} responded {StatusCode} in {ElapsedMs} ms.",
                    context.Request.Method,
                    path,
                    context.Response.StatusCode,
                    elapsedMs);
            }
        }
    }
}

/// <summary>Registration helper so Program.cs reads as a pipeline, not a type name.</summary>
public static class RequestTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestTimingMiddleware>();
}
