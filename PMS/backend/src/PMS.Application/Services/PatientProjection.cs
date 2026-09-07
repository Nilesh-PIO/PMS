using PMS.Application.Dtos.Patients;
using PMS.Domain.Entities;

namespace PMS.Application.Services;

/// <summary>
/// Turns a <see cref="Patient"/> entity into the DTOs the API returns
/// (planning-pms-verification.md, section 2 "DTOs separate from EF entities"; brainstorm REC-12,
/// E-8, E-20).
/// </summary>
/// <remarks>
/// <para>
/// Extracted at F-6 because a second service now returns a patient summary: F-5's registration and
/// F-6's <c>mark-merged</c> both answer with a <see cref="PatientResponse"/>, and F-7's picker will
/// be the third. Two private copies of this projection would eventually disagree about something
/// small — which fields count as missing, say — and the visible symptom would be one screen calling
/// a profile complete while another calls it incomplete.
/// </para>
/// <para>
/// Static and dependency-free on purpose: it is a pure mapping, and giving it a clock or a
/// repository would let it start deciding things.
/// </para>
/// </remarks>
public static class PatientProjection
{
    /// <summary>
    /// What is missing from a profile, named rather than counted (E-8, E-20).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ASSUMPTION (plan F-5 point 1, Q-7 / Q-16), carried over unchanged from F-5.</b> The plan
    /// says a patient saved without a phone is "flagged incomplete" but does not enumerate what
    /// makes a profile complete. Taken as the three fields later features actually depend on:
    /// <b>phone</b> (F-7 searches it and the clinic phones people with it), <b>age</b> (F-11 and
    /// F-14 need it for any dosing judgement) and <b>gender</b> (F-4 exists to make it recordable).
    /// </para>
    /// <para>
    /// The alternate contact is deliberately <em>not</em> in the set — it is genuinely optional
    /// extra, and including it would leave most well-filled profiles permanently flagged, which is
    /// how a warning becomes wallpaper.
    /// </para>
    /// <para>
    /// Nothing branches on this flag: it is displayed, never enforced.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> MissingFields(Patient patient)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(patient.PrimaryPhone))
        {
            missing.Add("phone");
        }

        if (patient.DateOfBirth is null && patient.ApproxAgeYears is null)
        {
            missing.Add("age");
        }

        if (string.IsNullOrWhiteSpace(patient.Gender))
        {
            missing.Add("gender");
        }

        return missing;
    }

    /// <summary>The summary shape — name plus the fields that tell two patients apart (REC-12).</summary>
    public static PatientResponse ToSummary(Patient patient, DateOnly today) =>
        new(
            patient.Id,
            patient.FullName,
            PatientNormalizer.PhoneTail(patient.PrimaryPhone),
            PatientAgeFormatter.Format(patient, today),
            patient.Gender,
            patient.Status.ToString(),
            MissingFields(patient).Count > 0);

    /// <summary>
    /// The duplicate-candidate shape (F-6 point 4). The same four disambiguating fields a picker
    /// row carries, plus what this row has in common with the person being registered.
    /// </summary>
    public static DuplicateCandidateResponse ToDuplicateCandidate(
        Patient patient,
        DateOnly today,
        string matchReason,
        double nameSimilarity,
        bool isLikelyDuplicate) =>
        new(
            patient.Id,
            patient.FullName,
            PatientNormalizer.PhoneTail(patient.PrimaryPhone),
            PatientAgeFormatter.Format(patient, today),
            patient.DateOfBirth,
            patient.Gender,
            patient.Status.ToString(),
            patient.MergedIntoPatientId,
            // Null until F-10 builds the Visit entity. Documented on the DTO; the field is on the
            // contract and rendered now so the picker row does not have to change later.
            LastVisitDate: null,
            matchReason,
            nameSimilarity,
            isLikelyDuplicate);
}
