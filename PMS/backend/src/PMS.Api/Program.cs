using PMS.Api.Middleware;
using PMS.Application;
using PMS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root. Each layer registers itself; Program.cs never news up a
// service or a DbContext by hand (planning-pms-verification.md, section 2).
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

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
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

// An unmatched /api/* path must return a ProblemDetails 404, never index.html - otherwise
// the React client would try to JSON-parse an HTML page and report a nonsense error.
app.MapFallback("/api/{**slug}", (HttpContext http) => Results.Problem(
    title: "Resource not found.",
    detail: "No API endpoint matches this route.",
    statusCode: StatusCodes.Status404NotFound,
    instance: http.Request.Path));

// Everything else is a client-side route owned by React Router.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>
/// Exposed so PMS.Api.IntegrationTests can use WebApplicationFactory&lt;Program&gt;.
/// </summary>
public partial class Program;
