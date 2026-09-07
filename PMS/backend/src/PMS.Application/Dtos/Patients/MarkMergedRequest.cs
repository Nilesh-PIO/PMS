namespace PMS.Application.Dtos.Patients;

/// <summary>
/// Records that one patient record is a duplicate of another
/// (planning-pms-verification.md, F-6 points 3 and 5; brainstorm E-26, E-33).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a pointer, not a merge.</b> Plan section 11 defers real merge tooling — combining two
/// visit histories, with undo and an audit trail — to Phase 2, and E-26 says plainly that the Phase-1
/// answer is "mark one record inactive with a <c>merged_into</c> pointer so both histories remain
/// readable and neither is deleted". Nothing this request causes is destructive: no row is removed,
/// no field is copied from one record to the other, and every visit attached to the marked record
/// stays attached to it and stays queryable.
/// </para>
/// <para>
/// It is written down rather than left implicit because the alternative — the physician remembering
/// which of two Ravi Kumars is the stale one — is exactly the knowledge that evaporates. The pointer
/// is what makes a real merge possible later; a deletion now would make it impossible forever.
/// </para>
/// </remarks>
public class MarkMergedRequest
{
    /// <summary>
    /// The record that survives — the one this patient's history should be read alongside.
    /// </summary>
    /// <remarks>
    /// Required. Must be a patient that exists, must not be the record being marked, and must not
    /// already point back at it: a cycle would leave neither record identifiable as the survivor,
    /// which is worse than no pointer at all.
    /// </remarks>
    public Guid MergedIntoPatientId { get; set; }

    /// <summary>
    /// Why this was judged a duplicate, in the physician's own words. Optional.
    /// </summary>
    /// <remarks>
    /// Stored on the marked record's <c>InactiveReason</c>. Optional because a required
    /// justification field is a field that gets filled with "dup", but prompted because the reason
    /// is the only part of this decision that cannot be reconstructed from the data afterwards.
    /// </remarks>
    public string? Note { get; set; }
}
