using PMS.Application.Abstractions;
using PMS.Application.Dtos.Patients;
using PMS.Application.Exceptions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Services;

/// <summary>
/// F-5. Registers patients and reads their profiles
/// (planning-pms-verification.md, F-5; brainstorm C-18, C-19, E-8, E-9, E-11, E-13, E-20, E-21,
/// E-43, E-46, E-57, E-59, E-60).
/// </summary>
/// <remarks>
/// <para>
/// <b>Integrity rule 1 — an age can never become a lie (E-9, C-19).</b> This is the one rule in the
/// feature that cannot be repaired after the fact. A bare number in an age column is a fact that
/// corrupts itself as time passes: a record reading "34" gives no way to recover whether that meant
/// 2026 or 2019, and no later migration can restore information that was never written down. So
/// exactly two shapes are accepted — a date of birth, or an approximate age together with the date
/// it was taken — and the rule is enforced twice, here and by a database check constraint, because
/// a rule this load-bearing should not depend on every future caller remembering to come through
/// this class.
/// </para>
/// <para>
/// <b>Integrity rule 2 — a thin record looks thin (E-8, E-20).</b> Everything except the name is
/// optional, because a patient with no phone is a real patient and refusing to register them is
/// worse than an incomplete record. But "allowed" is not "unremarkable": the profile reports
/// exactly what is missing, so the gap is visible on the screen rather than discovered later when
/// someone needs to phone them.
/// </para>
/// <para>
/// <b>Integrity rule 3 — the normalized columns are written here and nowhere else (E-59, E-60).</b>
/// The client never sends them and cannot set them; they are derived on every save. F-6's duplicate
/// detection and F-7's search both read them, so a client-supplied value would be a way to make
/// yourself invisible to the duplicate check.
/// </para>
/// <para>
/// <b>Integrity rule 4 — one submit is one patient (E-43, E-46).</b> See
/// <see cref="CreateAsync"/>: the guarantee is a unique index, not a look-before-you-leap read.
/// </para>
/// <para>
/// <b>What this class deliberately does not do: reject a plausible-looking name.</b> No length
/// floor beyond "not blank", no requirement for two words, no character-set rule. C-18 and E-13 are
/// explicit that a required-surname design rejects real patients, and every additional rule here is
/// another real person the front desk has to invent data for.
/// </para>
/// </remarks>
public sealed class PatientService : IPatientService
{
    /// <summary>
    /// Matches the <c>nvarchar(200)</c> column in plan section 4. A storage limit, not an opinion
    /// about names.
    /// </summary>
    public const int MaxNameLength = 200;

    /// <summary>Storage limit for the phone and alternate-contact columns.</summary>
    public const int MaxPhoneLength = 40;

    /// <summary>Storage limit for the alternate contact, which may be "Sister — 98765 43210".</summary>
    public const int MaxAltContactLength = 200;

    /// <summary>
    /// The oldest approximate age accepted. A storage and typo bound, <b>not</b> a clinical
    /// judgement: it exists so a mis-keyed "400" is caught, and it is set well beyond any
    /// documented human lifespan so it can never reject a real patient.
    /// </summary>
    public const int MaxApproxAgeYears = 150;

    private readonly IPatientRepository _patients;
    private readonly IClinicSettingsService _settings;
    private readonly IPatientDuplicateService _duplicates;
    private readonly IClock _clock;

    public PatientService(
        IPatientRepository patients,
        IClinicSettingsService settings,
        IPatientDuplicateService duplicates,
        IClock clock)
    {
        _patients = patients;
        _settings = settings;
        _duplicates = duplicates;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<PatientResponse> CreateAsync(
        CreatePatientRequest request,
        bool confirmDuplicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        // The replay fast path. Checked before validation as well as before insert: a retried
        // submit should return the patient it created even if the form it came from would no
        // longer validate (a gender option retired in between, say). The first submit is what
        // happened; the second is just asking what happened.
        if (request.SubmissionId is { } submissionId && submissionId != Guid.Empty)
        {
            var existing = await _patients.GetBySubmissionIdAsync(submissionId, cancellationToken);
            if (existing is not null)
            {
                return ToSummary(existing, today);
            }
        }

        var validated = await ValidateAsync(request, today, cancellationToken);

        // F-6, and the ordering here is the acceptance criterion, not an implementation detail.
        //
        // The check runs *after* validation and *before* the insert. After validation, because
        // warning about a duplicate on a form that is going to be rejected anyway wastes the
        // physician's attention on the wrong problem. Before the insert, because plan F-6
        // acceptance criterion 1 requires the 409 "before any row is written" - a check that fired
        // afterwards would be a duplicate report rather than a duplicate check, and the split
        // history would already exist by the time anyone read it.
        if (!confirmDuplicate)
        {
            var candidates = await _duplicates.FindCandidatesAsync(
                new DuplicateCheckRequest
                {
                    FullName = validated.FullName,
                    Phone = validated.PrimaryPhone,
                    DateOfBirth = validated.DateOfBirth,
                },
                cancellationToken);

            // Only a *likely* duplicate stops a registration. The check also returns household
            // context - other patients reachable on the same phone whose names are nothing alike
            // (E-27) - and interrupting a registration for those would train the physician to click
            // through the warning, which is the failure that makes the whole feature worthless.
            if (candidates.Any(c => c.IsLikelyDuplicate))
            {
                throw new DuplicatePatientException(candidates);
            }
        }

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FullName = validated.FullName,
            NormalizedName = PatientNormalizer.NormalizeName(validated.FullName),
            DateOfBirth = validated.DateOfBirth,
            ApproxAgeYears = validated.ApproxAgeYears,
            AgeRecordedOn = validated.AgeRecordedOn,
            Gender = validated.Gender,
            PrimaryPhone = validated.PrimaryPhone,
            NormalizedPhone = PatientNormalizer.NormalizePhone(validated.PrimaryPhone),
            // F-6's matching key, derived on the same save as the column it comes from. Deriving it
            // here rather than in the duplicate service is what keeps it in step with what is
            // stored: a key computed at query time would silently stop matching rows written
            // before the rule last changed.
            PhoneMatchKey = PatientNormalizer.PhoneMatchKey(validated.PrimaryPhone),
            AltContact = validated.AltContact,
            RegisteredUtc = _clock.UtcNow,
            Status = PatientStatus.Active,
            InactiveReason = null,
            MergedIntoPatientId = null,
            SubmissionId = request.SubmissionId == Guid.Empty ? null : request.SubmissionId,
        };

        await _patients.AddAsync(patient, cancellationToken);

        try
        {
            await _patients.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_patients.IsDuplicateSubmissionConflict(ex))
        {
            // Two clicks raced, both read nothing above, and both tried to insert. The unique index
            // is what actually decided; this branch is the loser learning the outcome.
            //
            // Re-reading rather than rethrowing matters: the physician clicked Save twice and one
            // patient was registered, which is exactly what they wanted. Surfacing a 409 here would
            // report a successful registration as a failure and invite them to try a third time.
            if (request.SubmissionId is { } racedId)
            {
                var winner = await _patients.GetBySubmissionIdAsync(racedId, cancellationToken);
                if (winner is not null)
                {
                    return ToSummary(winner, today);
                }
            }

            throw;
        }

        return ToSummary(patient, today);
    }

    /// <inheritdoc />
    public async Task<PatientDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var patient = await _patients.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), id.ToString());

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        return new PatientDetailResponse(
            patient.Id,
            patient.FullName,
            patient.NormalizedName,
            patient.DateOfBirth,
            patient.ApproxAgeYears,
            patient.AgeRecordedOn,
            PatientAgeFormatter.Format(patient, today),
            patient.Gender,
            patient.PrimaryPhone,
            PatientNormalizer.PhoneTail(patient.PrimaryPhone),
            patient.AltContact,
            patient.RegisteredUtc,
            patient.Status.ToString(),
            patient.InactiveReason,
            patient.MergedIntoPatientId,
            MissingFields(patient).Count > 0,
            MissingFields(patient));
    }

    // --- validation ---------------------------------------------------------

    private sealed record ValidatedPatient(
        string FullName,
        DateOnly? DateOfBirth,
        int? ApproxAgeYears,
        DateOnly? AgeRecordedOn,
        string? Gender,
        string? PrimaryPhone,
        string? AltContact);

    /// <summary>
    /// Validates the whole form and reports <em>every</em> problem at once.
    /// </summary>
    /// <remarks>
    /// Errors accumulate rather than throwing on the first one. A registration form has six fields
    /// and a physician with a patient in front of them; being told about one mistake per round trip
    /// is how a person ends up avoiding the form.
    /// </remarks>
    private async Task<ValidatedPatient> ValidateAsync(
        CreatePatientRequest request,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        // --- name (C-18, E-13) ---
        var fullName = Collapse(request.FullName);

        if (fullName.Length == 0)
        {
            errors[nameof(request.FullName)] = ["Enter the patient's name."];
        }
        else if (fullName.Length > MaxNameLength)
        {
            errors[nameof(request.FullName)] =
                [$"A name must be {MaxNameLength} characters or fewer."];
        }

        // No rule requiring a space, a surname, a minimum word count or a Latin script. A
        // single-word name is a valid name and a non-Latin name is a valid name; both are named in
        // the brainstorm as designs that reject real patients (E-13, E-57).

        // --- age (C-19, E-9, E-11, E-21) — integrity rule 1 ---
        var (dateOfBirth, approxAge, ageRecordedOn) = ValidateAge(request, today, errors);

        // --- gender (C-20, and F-4's list) ---
        var gender = Collapse(request.Gender);
        string? storedGender = null;

        if (gender.Length > 0)
        {
            // Asked of F-4's service rather than compared against a list here, so there is exactly
            // one definition of "an offered option" and retiring one takes effect everywhere at
            // once. `includeInactive` is not consulted: a retired option must not be selectable for
            // a *new* record, even though existing records keep displaying it.
            var offered = await _settings.IsOfferedOptionAsync(
                SettingCategory.Gender, gender, cancellationToken);

            if (!offered)
            {
                errors[nameof(request.Gender)] =
                [
                    $"\"{gender}\" is not one of the gender options this clinic offers. "
                    + "Choose one from the list, or add it under Settings.",
                ];
            }
            else
            {
                storedGender = gender;
            }
        }

        // --- contact (Q-7, E-20, E-59) ---
        // Optional by decision, not by omission: saving without a phone is allowed and the profile
        // says so afterwards. Only the storage length is checked; the *format* is never policed,
        // because E-59 is explicit that rejecting the formats the clinic uses is the wrong fix.
        var primaryPhone = Collapse(request.PrimaryPhone);
        if (primaryPhone.Length > MaxPhoneLength)
        {
            errors[nameof(request.PrimaryPhone)] =
                [$"A phone number must be {MaxPhoneLength} characters or fewer."];
        }

        var altContact = Collapse(request.AltContact);
        if (altContact.Length > MaxAltContactLength)
        {
            errors[nameof(request.AltContact)] =
                [$"An alternate contact must be {MaxAltContactLength} characters or fewer."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        return new ValidatedPatient(
            fullName,
            dateOfBirth,
            approxAge,
            ageRecordedOn,
            storedGender,
            primaryPhone.Length > 0 ? primaryPhone : null,
            altContact.Length > 0 ? altContact : null);
    }

    /// <summary>
    /// The age rule (E-9, E-11, E-21) — integrity rule 1, in one place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four accepted outcomes and no fifth:
    /// </para>
    /// <list type="number">
    ///   <item><description>a date of birth, today or earlier;</description></item>
    ///   <item><description>an approximate age plus the date it was taken;</description></item>
    ///   <item><description>an approximate age alone, which is <b>completed</b> with today's date
    ///   rather than rejected — the client normally omits the date and the server is the honest
    ///   place to stamp it;</description></item>
    ///   <item><description>nothing at all, which is a legitimately unknown age (E-8).</description></item>
    /// </list>
    /// <para>
    /// Rejected: both a date of birth and an approximate age (the record would then have two
    /// answers and no rule for which wins), a date of birth in the future, an
    /// <c>AgeRecordedOn</c> with no age to attach it to, and an age recorded in the future.
    /// </para>
    /// </remarks>
    private static (DateOnly? DateOfBirth, int? ApproxAgeYears, DateOnly? AgeRecordedOn) ValidateAge(
        CreatePatientRequest request,
        DateOnly today,
        IDictionary<string, string[]> errors)
    {
        var dateOfBirth = request.DateOfBirth;
        var approxAge = request.ApproxAgeYears;
        var recordedOn = request.AgeRecordedOn;

        if (dateOfBirth is not null && approxAge is not null)
        {
            errors[nameof(request.DateOfBirth)] =
            [
                "Give either a date of birth or an approximate age, not both. "
                + "A record with two different answers has no way to say which one is right.",
            ];
            return (null, null, null);
        }

        if (dateOfBirth is { } dob)
        {
            // E-11: today is accepted — a newborn seen on the day of birth is an ordinary
            // registration, and the formatter renders it in days.
            if (dob > today)
            {
                errors[nameof(request.DateOfBirth)] =
                    ["A date of birth cannot be in the future."];
                return (null, null, null);
            }

            if (recordedOn is not null)
            {
                errors[nameof(request.AgeRecordedOn)] =
                [
                    "A recorded-on date belongs with an approximate age. "
                    + "A date of birth does not need one.",
                ];
                return (null, null, null);
            }

            return (dob, null, null);
        }

        if (approxAge is { } age)
        {
            if (age < 0 || age > MaxApproxAgeYears)
            {
                errors[nameof(request.ApproxAgeYears)] =
                    [$"Enter an approximate age between 0 and {MaxApproxAgeYears}."];
                return (null, null, null);
            }

            var stampedOn = recordedOn ?? today;

            if (stampedOn > today)
            {
                errors[nameof(request.AgeRecordedOn)] =
                    ["An age cannot have been recorded in the future."];
                return (null, null, null);
            }

            // The pair, always. This is the line E-9 is about: the age and the date it was taken
            // are stored together or not at all.
            return (null, age, stampedOn);
        }

        if (recordedOn is not null)
        {
            errors[nameof(request.ApproxAgeYears)] =
            [
                "Enter the approximate age this date belongs to, or clear the date.",
            ];
            return (null, null, null);
        }

        // Nothing given. Allowed (E-8) and reported as missing on the profile.
        return (null, null, null);
    }

    // --- projection ---------------------------------------------------------

    /// <summary>
    /// What is missing from a profile (E-8, E-20). Delegates to <see cref="PatientProjection"/>.
    /// </summary>
    /// <remarks>
    /// Moved out of this class at F-6, when <c>mark-merged</c> became a second producer of a
    /// <see cref="PatientResponse"/>. The assumption behind the field list, and the reasoning for
    /// it, moved with it — one definition, so two screens cannot disagree about whether a profile
    /// is complete.
    /// </remarks>
    private static IReadOnlyList<string> MissingFields(Patient patient) =>
        PatientProjection.MissingFields(patient);

    private static PatientResponse ToSummary(Patient patient, DateOnly today) =>
        PatientProjection.ToSummary(patient, today);

    /// <summary>
    /// Trims and collapses internal whitespace on a value that will be <em>stored and displayed</em>
    /// (REC-18, E-60).
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="PatientNormalizer.NormalizeName"/>, which also case-folds and is
    /// only ever used for the matching column. Case is part of a person's name and is preserved
    /// here; stray whitespace is not, and collapsing it on the stored value is what makes
    /// <c>"  Ravi   Kumar  "</c> display as <c>"Ravi Kumar"</c> rather than merely match as it.
    /// </remarks>
    private static string Collapse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
