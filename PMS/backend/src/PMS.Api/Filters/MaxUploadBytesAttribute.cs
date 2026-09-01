using Microsoft.AspNetCore.Mvc.Filters;
using PMS.Application.Exceptions;

namespace PMS.Api.Filters;

/// <summary>
/// Rejects an upload larger than <see cref="MaxBytes"/> with a 413 before anything reads the body.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a resource filter and not a check inside the action.</b> MVC runs its value-provider
/// factories during model binding, and <c>FormValueProviderFactory</c> calls
/// <c>ReadFormAsync</c> for <em>any</em> action whose request has a form content type - whether or
/// not the action has a form-bound parameter. When that read trips the request-size limit, the
/// factory records "Failed to read the request form" in <c>ModelState</c>, and
/// <c>[ApiController]</c>'s automatic <c>ModelStateInvalidFilter</c> answers <b>400</b> without
/// the action ever running. A resource filter runs <em>before</em> model binding, so it is the
/// only place a size check can win that race.
/// </para>
/// <para>
/// This was found by a live run against Kestrel, not by the integration suite: under
/// <c>WebApplicationFactory</c>'s in-memory server the oversize request failed earlier and
/// happened to produce the right status. The check here is on <c>Content-Length</c> alone, which
/// behaves identically under both servers, so the test and the deployed behaviour cannot diverge
/// again.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class MaxUploadBytesAttribute : Attribute, IAsyncResourceFilter
{
    public MaxUploadBytesAttribute(int maxBytes)
    {
        MaxBytes = maxBytes;
    }

    /// <summary>The largest upload this endpoint accepts, in bytes.</summary>
    public int MaxBytes { get; }

    /// <summary>Message the 413 carries. Set by the endpoint so it can name the unit the user sees.</summary>
    public string Message { get; init; } = "The uploaded file is too large.";

    public Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var declaredLength = context.HttpContext.Request.ContentLength;

        // A multipart envelope is a little larger than the file inside it, so the comparison is
        // against the whole body. The action re-checks the decoded file against the real business
        // cap; this filter only exists to stop an oversize body from becoming a 400.
        if (declaredLength.HasValue && declaredLength.Value > MaxBytes)
        {
            throw new PayloadTooLargeException(Message, MaxBytes);
        }

        return next();
    }
}
