namespace PMS.Domain.Enums;

/// <summary>
/// The unit every temperature in this clinic is recorded and displayed in
/// (planning-pms-verification.md, section 4; brainstorm E-24).
/// </summary>
/// <remarks>
/// <para>
/// <b>E-24.</b> "37" and "98.6" are the same fever in different units, and a temperature stored
/// without its unit is a number nobody can safely act on. The unit is therefore a property of
/// the clinic, chosen once during first-run setup, and it travels with every stored value in UI
/// and print rather than being assumed by the reader.
/// </para>
/// <para>
/// ASSUMPTION (plan F-3 point 2, "TemperatureUnit is chosen"): that wording requires a
/// representable "not chosen yet" state, otherwise a freshly created row would silently claim
/// Celsius and satisfy the setup gate without the physician ever having answered. Hence
/// <see cref="Unspecified"/> at 0 - the CLR default for an int-backed enum - so an unanswered
/// column can never masquerade as an answer. It is not a selectable option in the UI.
/// </para>
/// </remarks>
public enum TemperatureUnit
{
    /// <summary>No unit has been chosen. Blocks <c>IsSetupComplete</c>.</summary>
    Unspecified = 0,

    /// <summary>Degrees Celsius (&#176;C).</summary>
    Celsius = 1,

    /// <summary>Degrees Fahrenheit (&#176;F).</summary>
    Fahrenheit = 2,
}
