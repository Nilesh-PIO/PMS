namespace PMS.Application.Dtos.Clinic;

/// <summary>
/// Request body for <c>PUT /api/clinic-settings/vital-ranges</c>
/// (planning-pms-verification.md, F-4 point 3).
/// </summary>
/// <remarks>
/// A partial list is accepted: only the metrics present are written, and a metric that is absent
/// keeps whatever it had. That is what lets the settings screen save one row at a time later
/// without having to resend thresholds it is not showing.
/// </remarks>
public class VitalRangeListRequest
{
    public List<VitalRangeItemRequest> Items { get; set; } = [];
}

/// <summary>One submitted threshold.</summary>
/// <remarks>
/// <para>
/// <b>Blank must arrive as <c>null</c>, not as <c>0</c>.</b> That distinction is the whole
/// feature: <c>null</c> means "do not warn me about this", while <c>0</c> is a threshold that
/// would warn on every pulse ever recorded. The frontend converts an empty input to <c>null</c>
/// and a test pins it, because a form that helpfully coerced <c>""</c> to <c>0</c> would silently
/// arm a warning on every value the clinic enters (plan F-4 point 6).
/// </para>
/// </remarks>
public class VitalRangeItemRequest
{
    /// <summary>
    /// The metric name, e.g. <c>"Temperature"</c>. An unknown or missing name is a 400 rather
    /// than a silently ignored row - a threshold the physician believes they saved and which was
    /// dropped on the floor is exactly the E-47 class of failure.
    /// </summary>
    public string? Metric { get; set; }

    /// <summary>Lower warning bound, or <c>null</c> for no lower warning.</summary>
    public decimal? WarnLow { get; set; }

    /// <summary>Upper warning bound, or <c>null</c> for no upper warning.</summary>
    public decimal? WarnHigh { get; set; }
}
