namespace PMS.Application.Dtos.Clinic;

/// <summary>
/// One entry in a doctor-configured list, as the API returns it
/// (planning-pms-verification.md, F-4 point 3).
/// </summary>
/// <param name="Id">Surrogate key. Sent so a settings screen can key a list row stably.</param>
/// <param name="Category">
/// The category <b>name</b> (<c>"Gender"</c>, <c>"VitalsNotRecordedReason"</c>), not its number.
/// <para>
/// ASSUMPTION (plan F-4 point 3 names the DTO but not its shape): categories travel as names
/// because the plan's own routes already use names - <c>?category=Gender</c> and
/// <c>PUT /options/{category}</c> - and a payload that answered <c>1</c> to a request that asked
/// for <c>Gender</c> would be two vocabularies for one concept. The column stays an <c>int</c>
/// for the reason F-3 gave for <c>TemperatureUnit</c>: a closed set the code branches on should
/// not be a free-text column a typo can corrupt.
/// </para>
/// </param>
/// <param name="Value">The value shown in the dropdown and stored on the patient/vitals row.</param>
/// <param name="DisplayOrder">1-based position in the dropdown.</param>
/// <param name="IsActive">
/// Whether the option is offered for new entries. Inactive options are still returned when the
/// caller asks for them, because the settings screen cannot re-activate an option it cannot see.
/// </param>
/// <param name="IsProtected">
/// Whether this option may never be deactivated or dropped from the list. True for
/// <c>Gender / Not stated</c> (E-23). Sent to the client so the row can render without a remove
/// control rather than offering an action the server will refuse - the server refuses it anyway.
/// </param>
public sealed record SettingOptionResponse(
    int Id,
    string Category,
    string Value,
    int DisplayOrder,
    bool IsActive,
    bool IsProtected);
