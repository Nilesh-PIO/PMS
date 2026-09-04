namespace PMS.Application.Dtos.Clinic;

/// <summary>
/// A soft plausibility warning: "this value is outside the range you told me to expect"
/// (planning-pms-verification.md, F-4 point 1; brainstorm E-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is advice, never a refusal.</b> Nothing in F-4 or F-11 turns a warning into a 400 or a
/// 409. The physician confirms and the value is saved exactly as entered - a genuine 41.8 degree
/// fever must be recordable, and software that refuses it teaches the user to type 39 instead.
/// That trade is stated in the brainstorm (E-12, REC-3) and it is the reason this type is a
/// message rather than an exception.
/// </para>
/// <para>
/// It is a DTO in the Application layer rather than something F-11 invents later because the plan
/// puts the "no warning when the threshold is blank" test in F-4's unit suite - so the decision
/// lives here, and F-11 renders what it is handed.
/// </para>
/// </remarks>
/// <param name="Metric">The metric name the warning is about.</param>
/// <param name="Label">The metric as the physician reads it.</param>
/// <param name="Value">The value that was entered, unmodified.</param>
/// <param name="WarnLow">The configured lower bound, if that is the one that was crossed.</param>
/// <param name="WarnHigh">The configured upper bound, if that is the one that was crossed.</param>
/// <param name="Message">
/// A complete sentence for the confirm dialog. Names the bound the physician set, so the warning
/// reads as "you asked me to flag this" rather than as a clinical opinion the software does not
/// have.
/// </param>
public sealed record VitalWarning(
    string Metric,
    string Label,
    decimal Value,
    decimal? WarnLow,
    decimal? WarnHigh,
    string Message);
