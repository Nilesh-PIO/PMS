using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Exceptions;

namespace PMS.Api.Middleware;

/// <summary>
/// The single error contract for the whole API (planning-pms-verification.md, section 7
/// "Error handling"). Every unhandled path out of a controller becomes an RFC-7807
/// ProblemDetails body so the React client has exactly one error shape to parse and
/// <c>httpClient.ts</c> can always throw a typed error - a swallowed failure is the E-47
/// failure mode ("doctor believes it saved"), so nothing here may return an empty body.
/// </summary>
public sealed class ProblemDetailsMiddleware
{
    /// <summary>Header carrying the correlation id, so a 500 in the UI can be traced to a log line.</summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>Extension key carrying the machine-readable domain rule slug on a 409.</summary>
    public const string RuleTypeExtension = "ruleType";

    /// <summary>Extension key carrying the correlation id inside the body as well as the header.</summary>
    public const string CorrelationIdExtension = "correlationId";

    private const string ContentType = "application/problem+json";

    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                // Nothing can be rewritten once bytes are on the wire; let it surface as a
                // broken response rather than corrupting a partially written body.
                _logger.LogError(ex, "Unhandled exception after the response had started.");
                throw;
            }

            var problem = Map(context, ex);
            await WriteAsync(context, problem);
        }
    }

    private ProblemDetails Map(HttpContext context, Exception exception)
    {
        switch (exception)
        {
            case ValidationFailedException validation:
                _logger.LogInformation("Validation failed for {Path}.", context.Request.Path);
                return new ValidationProblemDetails(
                    validation.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                    Instance = context.Request.Path,
                };

            case NotFoundException notFound:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Resource not found.",
                    Detail = notFound.Message,
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                    Instance = context.Request.Path,
                };

            case DomainRuleException rule:
                _logger.LogInformation(
                    "Domain rule {RuleType} rejected {Path}.", rule.RuleType, context.Request.Path);
                var ruleProblem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The request conflicts with a domain rule.",
                    Detail = rule.Message,
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                    Instance = context.Request.Path,
                };
                ruleProblem.Extensions[RuleTypeExtension] = rule.RuleType;
                return ruleProblem;

            case DbUpdateConcurrencyException:
                // Two tabs edited the same row. This must fail loudly rather than
                // last-write-wins, which would silently discard the other tab's edit.
                _logger.LogWarning("Concurrency conflict on {Path}.", context.Request.Path);
                var concurrencyProblem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The record was changed by someone else.",
                    Detail = "This record has changed since it was loaded. Reload and reapply the change.",
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                    Instance = context.Request.Path,
                };
                concurrencyProblem.Extensions[RuleTypeExtension] = "concurrency-conflict";
                return concurrencyProblem;

            default:
                var correlationId = GetOrCreateCorrelationId(context);
                _logger.LogError(
                    exception,
                    "Unhandled exception {CorrelationId} on {Method} {Path}.",
                    correlationId,
                    context.Request.Method,
                    context.Request.Path);

                var serverProblem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    // No exception message, stack trace or SQL is ever returned: those can
                    // contain PHI or deployment detail (section 7, Logging & audit).
                    Detail = "The request could not be completed. Quote the correlation id when reporting this.",
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
                    Instance = context.Request.Path,
                };
                serverProblem.Extensions[CorrelationIdExtension] = correlationId;
                return serverProblem;
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var supplied)
            && !string.IsNullOrWhiteSpace(supplied))
        {
            return supplied.ToString();
        }

        return Activity.Current?.Id ?? context.TraceIdentifier;
    }

    private static async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = ContentType;

        if (problem.Extensions.TryGetValue(CorrelationIdExtension, out var correlationId)
            && correlationId is string id)
        {
            context.Response.Headers[CorrelationIdHeader] = id;
        }

        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: ContentType);
    }
}

/// <summary>Registration helper so Program.cs reads as a pipeline, not a type name.</summary>
public static class ProblemDetailsMiddlewareExtensions
{
    public static IApplicationBuilder UsePmsProblemDetails(this IApplicationBuilder app) =>
        app.UseMiddleware<ProblemDetailsMiddleware>();
}
