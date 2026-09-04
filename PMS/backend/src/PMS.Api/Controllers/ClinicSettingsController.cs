using Microsoft.AspNetCore.Mvc;
using PMS.Application.Abstractions;
using PMS.Application.Dtos.Clinic;

namespace PMS.Api.Controllers;

/// <summary>
/// F-4's four endpoints (planning-pms-verification.md, F-4 point 3). Depends on
/// <see cref="IClinicSettingsService"/> and never on PmsDbContext (section 2, API shape).
/// </summary>
/// <remarks>
/// No <c>[AllowAnonymous]</c> anywhere: F-2's fallback policy is default-deny, so every route here
/// requires the session cookie exactly as the plan's table specifies. These are settings, not
/// patient data, but "which genders does this clinic offer" is still nobody's business but the
/// clinic's.
/// </remarks>
[ApiController]
[Route("api/clinic-settings")]
[Produces("application/json")]
public class ClinicSettingsController : ControllerBase
{
    private readonly IClinicSettingsService _settings;

    public ClinicSettingsController(IClinicSettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// The options in a category, in display order.
    /// </summary>
    /// <param name="category">
    /// <c>Gender</c> or <c>VitalsNotRecordedReason</c>. Required - an unknown or missing category
    /// is a 400 naming the ones that exist, never an empty 200 that would read as "you have
    /// configured nothing".
    /// </param>
    /// <param name="includeInactive">
    /// <c>false</c> by default, so a caller that just wants a dropdown gets only what may be
    /// offered. The settings screen passes <c>true</c> to see retired options and bring one back.
    /// </param>
    [HttpGet("options")]
    [ProducesResponseType(typeof(IReadOnlyList<SettingOptionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SettingOptionResponse>>> GetOptions(
        [FromQuery] string? category,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken) =>
        Ok(await _settings.GetOptionsAsync(category, includeInactive, cancellationToken));

    /// <summary>
    /// Replaces a category's list with the submitted one. 200 with the saved list (including
    /// retired entries, so the screen can render what it now holds), 400 on validation failure.
    /// </summary>
    /// <remarks>
    /// A <b>PUT</b> of the whole list rather than per-item POST/DELETE, exactly as the plan's route
    /// table specifies, and the reason is not just REST tidiness: reordering plus retiring is one
    /// intent, and splitting it into several requests would let it half-apply.
    /// <para>
    /// There is no DELETE route on this controller <b>at all</b>, and that absence is the feature
    /// (F-4 point 5). Patient rows store the option's text, so deleting a row would leave old
    /// records displaying a value that appears nowhere in settings. Omitting an option from the PUT
    /// retires it instead.
    /// </para>
    /// </remarks>
    [HttpPut("options/{category}")]
    [ProducesResponseType(typeof(IReadOnlyList<SettingOptionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SettingOptionResponse>>> SaveOptions(
        string category,
        [FromBody] SettingOptionListRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _settings.SaveOptionsAsync(category, request, cancellationToken));

    /// <summary>
    /// Every vital's plausibility threshold. Always the full metric list; a metric the physician
    /// has never configured comes back with both bounds <c>null</c>, which means no warning.
    /// </summary>
    [HttpGet("vital-ranges")]
    [ProducesResponseType(typeof(IReadOnlyList<VitalRangeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VitalRangeResponse>>> GetVitalRanges(
        CancellationToken cancellationToken) =>
        Ok(await _settings.GetVitalRangesAsync(cancellationToken));

    /// <summary>Saves the submitted thresholds. 200 with the full list, 400 on validation failure.</summary>
    [HttpPut("vital-ranges")]
    [ProducesResponseType(typeof(IReadOnlyList<VitalRangeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<VitalRangeResponse>>> SaveVitalRanges(
        [FromBody] VitalRangeListRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _settings.SaveVitalRangesAsync(request, cancellationToken));
}
