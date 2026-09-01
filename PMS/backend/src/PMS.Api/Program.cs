using PMS.Api.Auth;
using PMS.Api.Middleware;
using PMS.Api.Startup;
using PMS.Application;
using PMS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root. Each layer registers itself; Program.cs never news up a
// service or a DbContext by hand (planning-pms-verification.md, section 2).
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// F-2. Cookie authentication plus a default-deny authorization policy: every endpoint needs
// the cookie unless it says [AllowAnonymous].
builder.Services.AddPmsAuthentication();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// F-2. One-time credential seeding, before the first request is served. Never throws: see
// InitialUserSeedExtensions, which also records the committed-credential deviation.
await app.SeedInitialUserAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // HTTPS is a hard requirement: the F-2 auth cookie is Secure-only, and encryption in
    // transit is a BRD non-functional requirement (Doc_BRD.md, Security).
    app.UseHsts();
}

// Ordered deliberately: timing wraps everything so a failed request is still timed;
// ProblemDetails sits directly outside the endpoint so every throw becomes RFC-7807.
app.UseRequestTiming();
app.UsePmsProblemDetails();

app.UseHttpsRedirection();

// PMS.Api serves the built React bundle from wwwroot in every environment. This is the
// same-origin requirement of the section 2 cookie-auth decision, not a deployment
// convenience - a cross-origin SPA cannot use a SameSite=Strict cookie.
//
// Deliberately ahead of authentication: the bundle is the login screen. It contains no PHI,
// and gating it would leave an unauthenticated visitor with nothing to sign in from.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// An unmatched /api/* path must return a ProblemDetails 404, never index.html - otherwise
// the React client would try to JSON-parse an HTML page and report a nonsense error.
//
// AllowAnonymous keeps F-1's contract intact under F-2's default-deny policy: a path that is
// not an endpoint is a 404, not a 401. F-2 acceptance criterion 2 is about routes that exist.
app.MapFallback("/api/{**slug}", (HttpContext http) => Results.Problem(
    title: "Resource not found.",
    detail: "No API endpoint matches this route.",
    statusCode: StatusCodes.Status404NotFound,
    instance: http.Request.Path))
    .AllowAnonymous();

// Everything else is a client-side route owned by React Router. Anonymous for the same reason
// the static files above are: React Router's own RequireAuth guard decides what renders, and
// a 401 here would replace the SPA shell with an error body the client could not recover from.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

/// <summary>
/// Exposed so PMS.Api.IntegrationTests can use WebApplicationFactory&lt;Program&gt;.
/// </summary>
public partial class Program;
