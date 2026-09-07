namespace PMS.Application.Dtos.Clinic;

/// <summary>
/// Request body for <c>PUT /api/clinic-settings/options/{category}</c>
/// (planning-pms-verification.md, F-4 point 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole list is submitted, not a diff.</b> Reordering is the common edit, and a diff
/// protocol for "these four moved" is more code and more ways to be wrong than sending the list
/// the physician is looking at. <see cref="Items"/> order <em>is</em> the display order.
/// </para>
/// <para>
/// <b>Omitting an item deactivates it - it never deletes it.</b> That is the F-4 point 5
/// integrity rule: patient rows store the gender string itself, so removing the row would leave
/// historical records showing a value that appears nowhere in settings. The service turns an
/// omission into <c>IsActive = false</c> and keeps the row.
/// </para>
/// </remarks>
public class SettingOptionListRequest
{
    /// <summary>
    /// The list in the order it should appear. <c>DisplayOrder</c> is assigned from this order by
    /// the service and is never accepted from the client - two rows claiming slot 3 is not a state
    /// a form should be able to produce.
    /// </summary>
    public List<SettingOptionItemRequest> Items { get; set; } = [];
}

/// <summary>One submitted list entry.</summary>
public class SettingOptionItemRequest
{
    /// <summary>
    /// The option value. Matched against existing rows case-insensitively after trimming, so
    /// re-submitting <c>"male"</c> updates the existing <c>Male</c> row rather than creating a
    /// second spelling of the same thing (C-20).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Whether the option should be offered for new entries. Present in the list but
    /// <c>false</c> is how an option is retired while staying visible on the settings screen.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
