using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Api.Auth;
using PMS.Application.Abstractions;
using PMS.Application.Dtos.Auth;

namespace PMS.Api.Controllers;

/// <summary>
/// F-2's four endpoints (planning-pms-verification.md, F-2 point 3). Depends on
/// <see cref="IAuthService"/> and never on PmsDbContext (section 2, API shape).
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IClock _clock;

    public AuthController(IAuthService authService, IClock clock)
    {
        _authService = authService;
        _clock = clock;
    }

    /// <summary>
    /// Signs the physician in and issues the session cookie.
    /// 200 on success, 400 for a malformed request, 401 for a wrong credential.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<SessionResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        SignInAsync(request, cancellationToken);

    /// <summary>
    /// Re-authenticates in place from the screen-lock overlay and issues a fresh cookie.
    /// </summary>
    /// <remarks>
    /// Anonymous on purpose, and this is the point of the endpoint: it is reached precisely
    /// when the cookie may already have expired. It authenticates by credential, not by
    /// cookie, so the physician can prove who they are <em>without</em> the client navigating
    /// away from a half-typed consultation - which is what would discard the draft (E-41).
    /// </remarks>
    [HttpPost("reauth")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<SessionResponse>> Reauth(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        SignInAsync(request, cancellationToken);

    /// <summary>
    /// Reports the current session. 200 with the cookie, 401 without it.
    /// This is how the React client learns it is signed out without having to guess.
    /// </summary>
    [HttpGet("session")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Session(CancellationToken cancellationToken)
    {
        var userName = User.FindFirstValue(ClaimTypes.Name);
        var absoluteExpiry = AuthenticationSetup.ReadAbsoluteExpiry(User);

        if (string.IsNullOrEmpty(userName) || absoluteExpiry is null)
        {
            // Reached only if a cookie somehow authenticated without the claims we issue.
            // Fail closed rather than invent a session.
            return Unauthorized();
        }

        return Ok(await _authService.DescribeSessionAsync(userName, absoluteExpiry.Value, cancellationToken));
    }

    /// <summary>Clears the session cookie. 204 with no body.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private async Task<ActionResult<SessionResponse>> SignInAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        // A ValidationFailedException from the service becomes a 400 through F-1's
        // ProblemDetailsMiddleware; nothing is caught here.
        var result = await _authService.AuthenticateAsync(request, cancellationToken);

        if (!result.Succeeded || result.Session is null || result.SecurityStamp is null)
        {
            // One body for both "no such user" and "wrong password". Distinguishing them
            // would confirm the user name to anyone who can reach the login page.
            return Unauthorized();
        }

        var principal = AuthenticationSetup.BuildPrincipal(
            result.Session.UserName,
            result.Session.ExpiresUtc,
            result.SecurityStamp);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                // Session cookie, not a persistent one: closing the browser on the clinic PC
                // must end the session (E-62). The absolute cap is carried by the claim, so
                // it survives even if the handler's own expiry is renewed by sliding.
                IsPersistent = false,
                IssuedUtc = _clock.UtcNow,
                AllowRefresh = true,
            });

        return Ok(result.Session);
    }
}
