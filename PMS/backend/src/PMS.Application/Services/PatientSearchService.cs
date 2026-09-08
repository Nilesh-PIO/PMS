using PMS.Application.Abstractions;
using PMS.Application.Dtos.Patients;
using PMS.Application.Exceptions;
using PMS.Domain.Entities;

namespace PMS.Application.Services;

/// <summary>
/// F-7. Finds a patient by name or phone, and lists the ones most recently dealt with
/// (planning-pms-verification.md, F-7; BRD L93 "Search patients by name or phone number" and
/// L157-159 "Quick patient search / View recent patients"; brainstorm C-22, C-35, E-2, E-7, E-28,
/// E-30, E-59, RSK-12, REC-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Integrity rule 1 — a row is never offered without the fields needed to tell people apart
/// (E-28, RSK-12).</b> This is the feature's dominant risk and it is not a cosmetic one: choosing
/// the wrong "Ravi Kumar" from a list attaches a consultation, a prescription and a history to the
/// wrong human being. The mechanism is that
/// <see cref="PatientSummaryResponse"/> has no name-only shape to construct — phone tail, age and a
/// date are all non-optional positions in the record, filled here for every row including the fuzzy
/// ones.
/// </para>
/// <para>
/// <b>Integrity rule 2 — nothing is ever auto-selected.</b> This service returns a list, always,
/// even when that list has one element. There is no "if exactly one match, return the patient"
/// branch and there must never be one: the single confident-looking match is precisely how the
/// wrong record gets opened without anyone reading it.
/// </para>
/// <para>
/// <b>Integrity rule 3 — an empty result is an answer, not a dead end (E-7, E-30).</b> Finding
/// nothing is the moment a duplicate patient gets created, so the empty path does two things rather
/// than none: it runs the similarity fallback, and every row it returns is labelled
/// <see cref="PatientMatchKind.SimilarName"/> so a guess is never rendered as a certainty. The
/// client turns the still-empty case into "register this typed name", which is the other half of
/// E-7.
/// </para>
/// <para>
/// <b>Integrity rule 4 — retired and merged records are out of the way by default.</b> See
/// <c>PatientSearchRules.IsSelectable</c>. They remain reachable behind an explicit toggle, and
/// arrive carrying their status, because "cannot be found at all" is its own way of causing a
/// duplicate.
/// </para>
/// </remarks>
public sealed class PatientSearchService : IPatientSearchService
{
    private readonly IPatientRepository _patients;
    private readonly IClock _clock;

    public PatientSearchService(IPatientRepository patients, IClock clock)
    {
        _patients = patients;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientSummaryResponse>> SearchAsync(
        string? query,
        bool includeInactive,
        int? take,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = PatientNormalizer.NormalizeName(query);

        // Length is measured on the *normalized* text, so three spaces is not a two-character
        // query and "  a " is not a four-character one.
        if (normalizedQuery.Length < PatientSearchRules.MinimumQueryLength)
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["query"] =
                [
                    "Type at least "
                    + PatientSearchRules.MinimumQueryLength
                    + " characters to search for a patient.",
                ],
            });
        }

        var digits = PatientNormalizer.NormalizePhone(query);
        var limit = PatientSearchRules.ClampTake(take, PatientSearchRules.DefaultTake);
        var today = Today();

        var specification = new PatientSearchSpecification(
            PatientSearchRules.Matches(includeInactive, normalizedQuery, digits),
            PatientSearchRules.Rank(normalizedQuery, digits),
            limit);

        var matches = await _patients.SearchAsync(specification, cancellationToken);

        if (matches.Count > 0)
        {
            return matches
                .Select(p => ToSummary(p, today, MatchKindOf(p, normalizedQuery, digits)))
                .ToList();
        }

        // E-30. Only here — after the indexed search has genuinely found nothing.
        return await FuzzyFallbackAsync(
            normalizedQuery, includeInactive, limit, today, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientSummaryResponse>> GetRecentAsync(
        int? take,
        CancellationToken cancellationToken)
    {
        var limit = PatientSearchRules.ClampTake(take, PatientSearchRules.DefaultRecentTake);
        var today = Today();

        var recent = await _patients.GetRecentAsync(limit, cancellationToken);

        // No empty-list special case. An empty list is the correct answer on a fresh install and the
        // client renders it as an empty state with a "register the first patient" action (E-2);
        // inventing a 404 or an error here would turn a normal state into a failure.
        return recent
            .Select(p => ToSummary(p, today, PatientMatchKind.Recent))
            .ToList();
    }

    // --- fuzzy fallback (E-30) ----------------------------------------------

    /// <summary>
    /// Names similar to the query, when nothing matched exactly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two round trips, both narrow: score every selectable name from two columns, then load the
    /// few survivors. That ordering is what keeps this affordable — the expensive part is the
    /// scoring, and it runs over short strings in memory rather than over materialised entities.
    /// </para>
    /// <para>
    /// <b>Why in memory rather than in SQL.</b> SQL Server has no similarity function, so the
    /// alternatives are a CLR assembly or a trigram shadow table. Both are real schema and
    /// deployment work in exchange for speeding up a path that (a) only runs after an exact search
    /// found nothing and (b) operates on a few thousand short strings. If the clinic's table grows
    /// far past the plan's 5,000-patient design point, this is the method to revisit — it is
    /// deliberately the only place in the feature that touches more than <c>take</c> rows.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<PatientSummaryResponse>> FuzzyFallbackAsync(
        string normalizedQuery,
        bool includeInactive,
        int limit,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var keys = await _patients.GetNameKeysAsync(includeInactive, cancellationToken);

        var scored = keys
            .Select(k => (k.Id, Score: NameSimilarity.Score(normalizedQuery, k.NormalizedName)))
            .Where(x => x.Score >= NameSimilarity.SearchFallbackThreshold)
            .OrderByDescending(x => x.Score)
            // Id as the final tie-break so two equally-similar names come back in the same order
            // every time. A picker that reshuffles between identical queries is a picker the
            // physician has to re-read.
            .ThenBy(x => x.Id)
            .Take(limit)
            .ToList();

        if (scored.Count == 0)
        {
            return [];
        }

        var byId = (await _patients.GetByIdsAsync([.. scored.Select(x => x.Id)], cancellationToken))
            .ToDictionary(p => p.Id);

        return scored
            .Where(x => byId.ContainsKey(x.Id))
            .Select(x => ToSummary(byId[x.Id], today, PatientMatchKind.SimilarName))
            .ToList();
    }

    // --- projection ----------------------------------------------------------

    /// <summary>
    /// Why a row is in the result, decided from the same rules the ranking used.
    /// </summary>
    /// <remarks>
    /// Recomputed here rather than carried out of SQL alongside the rank. The alternative is a
    /// projection type and a second expression to keep in step with the first, for a value that is
    /// two string comparisons over at most fifty rows already in memory.
    /// </remarks>
    private static string MatchKindOf(Patient patient, string normalizedQuery, string? digits)
    {
        if (patient.NormalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return PatientMatchKind.Name;
        }

        if (digits is not null
            && patient.NormalizedPhone is { } phone
            && phone.Contains(digits, StringComparison.Ordinal))
        {
            return PatientMatchKind.Phone;
        }

        // Reachable only if the store returned a row the rules do not explain. Reporting it as a
        // name match would be a small lie; "SimilarName" is the honest label for "we are not
        // certain why this is here", and it is the one the UI treats with suspicion.
        return PatientMatchKind.SimilarName;
    }

    /// <summary>
    /// The picker row. Every disambiguating field is filled for every row, on every path.
    /// </summary>
    private static PatientSummaryResponse ToSummary(Patient patient, DateOnly today, string matchKind) =>
        new(
            patient.Id,
            patient.FullName,
            PatientNormalizer.PhoneTail(patient.PrimaryPhone),
            PatientAgeFormatter.Format(patient, today),
            patient.Gender,
            // ASSUMPTION (plan F-7 point 2): null until F-10 introduces Visit. See
            // PatientSummaryResponse.LastVisitDate - the field ships now so that F-10 fills a
            // value in rather than changing a contract, and RegisteredOn carries the date axis
            // E-28 needs in the meantime.
            LastVisitDate: null,
            DateOnly.FromDateTime(patient.RegisteredUtc.UtcDateTime),
            patient.Status.ToString(),
            patient.MergedIntoPatientId is not null,
            IsProfileIncomplete(patient),
            matchKind);

    /// <summary>
    /// Whether the profile is missing something worth chasing (E-8, E-20).
    /// </summary>
    /// <remarks>
    /// The same three fields <c>PatientService</c> reports on the profile — phone, age, gender.
    /// <b>Flagged for the reviewer:</b> the rule is now stated in two places. It is not extracted
    /// into a shared helper here because <c>PatientService.MissingFields</c> is private to F-5 and
    /// changing its shape is F-8's edit-path territory, not a change F-7 should make to a verified
    /// feature in passing. <c>PatientSearchServiceTests</c> asserts the two agree, so a future
    /// change to one without the other fails a test rather than quietly making the picker disagree
    /// with the profile it opens.
    /// </remarks>
    private static bool IsProfileIncomplete(Patient patient) =>
        string.IsNullOrWhiteSpace(patient.PrimaryPhone)
        || (patient.DateOfBirth is null && patient.ApproxAgeYears is null)
        || string.IsNullOrWhiteSpace(patient.Gender);

    private DateOnly Today() => DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
}
