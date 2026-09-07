using PMS.Application.Abstractions;
using PMS.Application.Dtos.Patients;
using PMS.Application.Exceptions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Services;

/// <summary>
/// F-6. Detects that a patient may already be on file, and records a non-destructive duplicate
/// pointer when the physician says they are
/// (planning-pms-verification.md, F-6; brainstorm REC-2, REC-12, RSK-2, E-25, E-26, E-27, E-28,
/// E-30, E-33).
/// </summary>
/// <remarks>
/// <para>
/// <b>The problem this feature exists for.</b> RSK-2 is "no patient identity rule — histories split
/// across duplicate records, decisions made on half a history", rated Critical and marked a blocker.
/// It is not a tidiness problem. A patient registered twice has their allergies on one record and
/// their current medication on the other, and the physician reading either one is reading a
/// half-history without being told it is half.
/// </para>
/// <para>
/// <b>The identity rule (plan F-6 point 1, Q-13).</b> A candidate is flagged when the normalized
/// names are at least <see cref="NameSimilarity.DefaultThreshold"/> alike <b>and</b> the records
/// share either a phone matching key or a date of birth. Name alone is deliberately not enough:
/// E-28 says two patients with the same name and the same age is an ordinary occurrence in one
/// clinic, and a rule that warned on every repeated name would be dismissed within a week and then
/// be worth nothing on the day it was right.
/// </para>
/// <para>
/// <b>It warns and never blocks (REC-2).</b> <see cref="FindCandidatesAsync"/> answers a question
/// and writes nothing. The 409 raised on the registration path is answered with
/// <c>?confirmDuplicate=true</c> and the answer is always accepted. The reasoning is in the
/// brainstorm and worth restating: fuzzy matching produces false positives, and a blocking rule
/// turns a false positive into a patient who cannot be registered — at which point the front desk
/// invents a spelling, and the result is a duplicate that is also unsearchable.
/// </para>
/// <para>
/// <b>Nothing here destroys anything (plan F-6 point 5, E-26, E-33).</b> <see cref="MarkMergedAsync"/>
/// writes a pointer and a status; it copies no field from one record to the other, removes no row,
/// and leaves every visit attached to the record it was attached to. Real merge tooling is Phase 2
/// (plan section 11), and it stays possible precisely because this feature refuses to do it badly
/// now.
/// </para>
/// </remarks>
public sealed class PatientDuplicateService : IPatientDuplicateService
{
    /// <summary>
    /// How many rows the exact-equality pre-filter will pull back before name scoring.
    /// </summary>
    /// <remarks>
    /// Generous rather than tight. E-27 is explicit that one phone number belongs to a whole
    /// household and that every match must be shown, so a limit that silently truncated a family
    /// would defeat the case it is meant to serve. Fifty is far past any real household and still
    /// bounds the work if a placeholder number ever ends up on hundreds of records.
    /// </remarks>
    public const int CandidateLimit = 50;

    /// <summary>
    /// How far <see cref="MarkMergedAsync"/> will walk a merge chain looking for a cycle before
    /// giving up.
    /// </summary>
    /// <remarks>
    /// A bound rather than a <c>while (true)</c>: if a cycle somehow already exists in the data —
    /// written by a script, or by a bug this code does not have yet — an unbounded walk would hang
    /// the request rather than reporting the problem. Thirty-two is orders of magnitude past any
    /// plausible chain.
    /// </remarks>
    public const int MaxMergeChainDepth = 32;

    /// <summary>Storage limit for the merge note, matching the InactiveReason column.</summary>
    public const int MaxNoteLength = 500;

    private readonly IPatientRepository _patients;
    private readonly IClock _clock;

    public PatientDuplicateService(IPatientRepository patients, IClock clock)
    {
        _patients = patients;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DuplicateCandidateResponse>> FindCandidatesAsync(
        DuplicateCheckRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedName = PatientNormalizer.NormalizeName(request.FullName);

        // No name, no rule. Every branch of the identity rule requires a name similarity, so there
        // is nothing to compute rather than nothing found.
        if (normalizedName.Length == 0)
        {
            return [];
        }

        var phoneMatchKey = PatientNormalizer.PhoneMatchKey(request.Phone);
        var dateOfBirth = request.DateOfBirth;

        // Neither half of "(phone OR date of birth)" is available, so no candidate could satisfy
        // the rule even if one existed.
        //
        // This is the rule's known blind spot, and it is the plan's rule rather than a shortcut
        // taken here: a patient registered twice with no phone and no date of birth on either
        // record is not detected. The alternative — warning on name alone — is what E-28 rules out.
        // Recorded rather than quietly widened; see the tracker note for Q-13.
        if (phoneMatchKey is null && dateOfBirth is null)
        {
            return [];
        }

        var pool = await _patients.FindDuplicateCandidatesAsync(
            phoneMatchKey, dateOfBirth, request.ExcludePatientId, cancellationToken);

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var matches = new List<DuplicateCandidateResponse>();

        foreach (var candidate in pool)
        {
            // Belt and braces: the repository filters on these, but the scoring below decides what
            // MatchReason says, so it has to establish the facts itself rather than assume them.
            var phoneMatches = phoneMatchKey is not null
                && string.Equals(candidate.PhoneMatchKey, phoneMatchKey, StringComparison.Ordinal);

            var dobMatches = dateOfBirth is not null && candidate.DateOfBirth == dateOfBirth;

            if (!phoneMatches && !dobMatches)
            {
                continue;
            }

            var similarity = NameSimilarity.Ratio(normalizedName, candidate.NormalizedName);

            // Note what is deliberately *not* here: a `continue` for names below the threshold.
            //
            // Rows sharing the phone but not the name stay in the result, flagged as not-likely.
            // That is what makes acceptance criterion 3 and E-27 true - a phone shared by three
            // family members returns all three - while the narrower identity rule still decides
            // which of them is a suspected duplicate. Dropping them here would have been one line
            // shorter and would have silently removed the household context the physician is being
            // asked to judge against.
            var isLikelyDuplicate = similarity >= NameSimilarity.DefaultThreshold;

            var reason = (phoneMatches, dobMatches) switch
            {
                (true, true) => DuplicateMatchReason.PhoneAndDateOfBirth,
                (true, false) => DuplicateMatchReason.Phone,
                _ => DuplicateMatchReason.DateOfBirth,
            };

            matches.Add(PatientProjection.ToDuplicateCandidate(
                candidate, today, reason, similarity, isLikelyDuplicate));
        }

        // Suspected duplicates first, then the strongest signal, then the closest name.
        // Deterministic ordering matters more than it looks: the physician reads this list top-down
        // under time pressure, and a list that reorders itself between two identical checks is a
        // list they stop trusting.
        //
        // Ordering is the only ranking this feature does. Nothing is auto-selected and nothing is
        // marked as "the" match (E-27, E-28, acceptance criterion 3).
        return
        [
            .. matches
                .OrderByDescending(m => m.IsLikelyDuplicate)
                .ThenByDescending(m => m.MatchReason == DuplicateMatchReason.PhoneAndDateOfBirth)
                .ThenByDescending(m => m.NameSimilarity)
                .ThenBy(m => m.FullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.Id)
        ];
    }

    /// <inheritdoc />
    public async Task<PatientResponse> MarkMergedAsync(
        Guid patientId,
        MarkMergedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var patient = await _patients.GetByIdAsync(patientId, cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), patientId.ToString());

        var note = Collapse(request.Note);
        var errors = new Dictionary<string, string[]>();

        if (request.MergedIntoPatientId == Guid.Empty)
        {
            errors[nameof(request.MergedIntoPatientId)] =
                ["Choose the record this patient should be merged into."];
        }
        else if (request.MergedIntoPatientId == patientId)
        {
            errors[nameof(request.MergedIntoPatientId)] =
            [
                "A record cannot be merged into itself. "
                + "Choose the other record — the one whose history should be read as current.",
            ];
        }

        if (note.Length > MaxNoteLength)
        {
            errors[nameof(request.Note)] = [$"A note must be {MaxNoteLength} characters or fewer."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        var survivor = await _patients.GetByIdAsync(request.MergedIntoPatientId, cancellationToken);

        if (survivor is null)
        {
            // A 400 rather than a 404: the missing thing is a value in the submitted body, not the
            // resource the URL addressed, and the caller's fix is to pick a different record.
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                [nameof(request.MergedIntoPatientId)] = ["That patient record no longer exists."],
            });
        }

        // Already pointed at this exact record. Answer with the record rather than a conflict: the
        // caller asked for a state that is already true, and reporting that as a failure would make
        // a retried request look like a broken one.
        if (patient.MergedIntoPatientId == survivor.Id)
        {
            return PatientProjection.ToSummary(
                patient, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));
        }

        // Already pointed somewhere else. Re-pointing would overwrite a decision the physician
        // recorded, and this feature does not rewrite anything (plan F-6 point 5). Phase-2 merge
        // tooling is where "undo a merge" belongs, with the audit trail that needs.
        if (patient.MergedIntoPatientId is { } existing)
        {
            throw new DomainRuleException(
                "already-merged",
                $"This record is already marked as a duplicate of another patient ({existing}). "
                + "Un-marking a merge is not something this version can do without losing the "
                + "original decision.");
        }

        await GuardAgainstCycleAsync(patient.Id, survivor, cancellationToken);

        // The whole write. Two fields, both additive: a pointer and a status. Nothing is copied
        // between records, nothing is cleared, and every visit attached to this patient stays
        // attached to this patient and stays queryable (E-26, E-33).
        patient.MergedIntoPatientId = survivor.Id;
        patient.Status = PatientStatus.Inactive;
        patient.InactiveReason = BuildInactiveReason(survivor, note);

        await _patients.SaveChangesAsync(cancellationToken);

        return PatientProjection.ToSummary(patient, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));
    }

    /// <summary>
    /// Refuses a pointer that would make a merge chain loop back on itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A cycle is worse than no pointer. The pointer's only job is to answer "which of these records
    /// is the current one", and in a cycle every record answers "that one" and none of them is
    /// current — so the history the physician is trying to reunite becomes unreachable from either
    /// end. It also makes the Phase-2 merge impossible to write, since there is no survivor to merge
    /// into.
    /// </para>
    /// <para>
    /// The walk follows the survivor's own chain. Marking A into B is illegal if B already points
    /// at A, and equally if B points at C which points at A — the second case is the one a
    /// single-step check would miss.
    /// </para>
    /// </remarks>
    private async Task GuardAgainstCycleAsync(
        Guid patientId,
        Patient survivor,
        CancellationToken cancellationToken)
    {
        var step = survivor;

        for (var depth = 0; depth < MaxMergeChainDepth; depth++)
        {
            if (step.Id == patientId)
            {
                throw new DomainRuleException(
                    "merge-cycle",
                    "That record already points back at this one, directly or through another "
                    + "record. Merging them this way would leave neither one identifiable as the "
                    + "current record.");
            }

            if (step.MergedIntoPatientId is not { } next)
            {
                return;
            }

            var following = await _patients.GetByIdAsync(next, cancellationToken);

            if (following is null)
            {
                // A dangling pointer. Not this request's problem to fix, and not a reason to refuse
                // it - the chain simply ends here, so there is no cycle to find.
                return;
            }

            step = following;
        }

        throw new DomainRuleException(
            "merge-chain-too-long",
            "This chain of merged records is longer than expected and may already loop. "
            + "It needs to be looked at directly before another record is added to it.");
    }

    /// <summary>
    /// The retirement reason written on the marked record.
    /// </summary>
    /// <remarks>
    /// <b>ASSUMPTION (plan F-6 point 3).</b> The plan's request carries a <c>note</c> but does not
    /// say where it is stored. It goes on <c>InactiveReason</c> — the column F-8 uses for the same
    /// purpose — with a fixed prefix naming the survivor, so a reader of that column months later
    /// can tell a merge apart from an ordinary deactivation without having to consult the pointer.
    /// The physician's own words are appended verbatim and never in place of the prefix.
    /// </remarks>
    private static string BuildInactiveReason(Patient survivor, string note)
    {
        var prefix = $"Marked as a duplicate of {survivor.FullName}.";

        var reason = note.Length == 0 ? prefix : $"{prefix} {note}";

        return reason.Length <= MaxNoteLength ? reason : reason[..MaxNoteLength];
    }

    private static string Collapse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
