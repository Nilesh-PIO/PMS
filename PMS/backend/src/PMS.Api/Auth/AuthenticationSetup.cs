using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using PMS.Application.Abstractions;
using PMS.Application.Services;

namespace PMS.Api.Auth;

/// <summary>
/// Registers F-2's cookie authentication and the default-deny authorization policy.
/// </summary>
/// <remarks>
/// Section 2's auth decision, implemented literally: a <c>HttpOnly</c>, <c>Secure</c>,
/// <c>SameSite=Strict</c> cookie and no token anywhere the page's own JavaScript can read it.
/// The consulting-room machine is the threat model (E-62, E-65) - anything in
/// <c>localStorage</c> outlives the session and survives the browser being closed.
/// </remarks>
public static class AuthenticationSetup
{
    public static IServiceCollection AddPmsAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = PmsAuthDefaults.CookieName;

                // The three attributes F-2 acceptance criterion 1 names, set explicitly rather
                // than left to a framework default that could change under us.
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;

                // Authentication is not a tracking cookie; it is exempt from consent gating.
                options.Cookie.IsEssential = true;
                options.Cookie.Path = "/";

                // Sliding renewal on activity (REC-11). The hard 12-hour cap is enforced by
                // OnValidatePrincipal below, not by this window, because the cookie handler's
                // sliding expiry on its own would renew forever.
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = SessionPolicy.SlidingWindow;

                // No "remember me": the cookie dies with the browser session as well as with
                // the absolute expiry. A persistent cookie on a shared clinic PC is E-62.
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = EnforceAbsoluteExpiryAsync,
                    OnRedirectToLogin = context => ChallengeAsync(context, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = context => ChallengeAsync(context, StatusCodes.Status403Forbidden),
                };
            });

        services.AddAuthorization(options =>
        {
            // Default deny. Every endpoint requires the cookie unless it opts out with
            // [AllowAnonymous], so a new controller added by a later feature is protected by
            // omission rather than exposed by it - the safe direction for a PHI system
            // (F-2 acceptance criterion 2).
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    /// <summary>
    /// Reads the absolute expiry stamped into a principal at sign-in.
    /// </summary>
    public static DateTimeOffset? ReadAbsoluteExpiry(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(PmsAuthDefaults.AbsoluteExpiryClaim);

        if (raw is null
            || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    /// <summary>
    /// Builds the claims a signed-in physician carries. The password and its hash are never
    /// among them.
    /// </summary>
    public static ClaimsPrincipal BuildPrincipal(
        string userName,
        DateTimeOffset absoluteExpiryUtc,
        string securityStamp)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, userName),
                new Claim(
                    PmsAuthDefaults.AbsoluteExpiryClaim,
                    absoluteExpiryUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                new Claim(PmsAuthDefaults.SecurityStampClaim, securityStamp),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Kills a session the instant it passes its absolute expiry, whatever the sliding window
    /// says. Without this, "sliding renewal" would mean a browser left open in a consulting
    /// room stays authenticated indefinitely.
    /// </summary>
    private static async Task EnforceAbsoluteExpiryAsync(CookieValidatePrincipalContext context)
    {
        var clock = context.HttpContext.RequestServices.GetRequiredService<IClock>();
        var absoluteExpiry = context.Principal is null ? null : ReadAbsoluteExpiry(context.Principal);

        // A cookie with no absolute expiry claim predates this policy or was tampered with.
        // Reject it rather than treat a missing cap as "no cap".
        if (absoluteExpiry is null || clock.UtcNow >= absoluteExpiry.Value)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    /// <summary>
    /// Answers an unauthenticated API call with a status code and an RFC-7807 body, never a
    /// 302 to an HTML login page. A redirect here would make <c>httpClient.ts</c> parse HTML
    /// as JSON and report a nonsense error instead of "your session ended" (E-47, and F-1's
    /// error contract).
    /// </summary>
    private static async Task ChallengeAsync(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        var response = context.Response;

        if (response.HasStarted)
        {
            return;
        }

        response.Clear();
        response.StatusCode = statusCode;
        response.ContentType = "application/problem+json";

        var problem = statusCode == StatusCodes.Status401Unauthorized
            ? new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2",
                title = "Your session has ended.",
                status = statusCode,
                detail = "Sign in again to continue. Nothing you were working on has been discarded.",
                instance = context.Request.Path.Value,
            }
            : new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                title = "You do not have access to this.",
                status = statusCode,
                detail = "This account is not permitted to perform that action.",
                instance = context.Request.Path.Value,
            };

        await response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
