using System.Linq.Expressions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Services;

/// <summary>
/// What counts as a match, and in what order matches are offered
/// (planning-pms-verification.md, F-7 point 1; brainstorm C-22, C-35, E-59, RSK-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>The search semantics live here, once, as expressions — and that is the point.</b> The same
/// two expressions are handed to EF Core, where they become the <c>WHERE</c> and <c>ORDER BY</c> of
/// a single SQL statement, <em>and</em> compiled and run in memory by the unit tests. There is no
/// second copy of the rule written in LINQ-to-Objects for testing and in SQL for production, so the
/// two cannot drift apart — which matters because the drift would be silent and would show up as
/// "the search finds them on my machine".
/// </para>
/// <para>
/// It also keeps the search definition in the <b>Application</b> layer rather than the repository:
/// C-22 is a product decision the plan owner still has to confirm, and it should be readable and
/// changeable without opening an EF Core file.
/// </para>
/// <para>
/// <b>ASSUMPTION (plan F-7 point 1, C-22 — brainstorm §12 carries no <c>Q-</c> for it).</b> Built
/// exactly to the plan's stated assumption: case-insensitive substring on <c>NormalizedName</c>,
/// digits-only match on <c>NormalizedPhone</c> including a last-4 suffix, minimum query length 2,
/// ranked exact → prefix → substring → phone, inactive and merged excluded by default. Every one of
/// those five decisions is pinned by a test in <c>PatientSearchServiceTests</c>.
/// </para>
/// </remarks>
public static class PatientSearchRules
{
    /// <summary>
    /// Shortest query that is allowed to run (C-22/C-35: "minimum query length 2").
    /// </summary>
    /// <remarks>
    /// A one-character query matches most of the table and tells the physician nothing, while
    /// costing a full scan on every keystroke. Two is the plan's number. Below it the API answers
    /// 400 rather than returning everything, because "everything" in a picker is exactly the
    /// name-only wall of rows that RSK-12 is about.
    /// </remarks>
    public const int MinimumQueryLength = 2;

    /// <summary>Default page size — the plan's server-side <c>TOP 20</c>.</summary>
    public const int DefaultTake = 20;

    /// <summary>Default size of the recent-patients list (plan F-7 point 3: <c>take=10</c>).</summary>
    public const int DefaultRecentTake = 10;

    /// <summary>
    /// Hard ceiling on <c>take</c>. A picker is for choosing one person, not for browsing.
    /// </summary>
    public const int MaxTake = 50;

    // --- rank buckets -------------------------------------------------------
    //
    // Lower sorts first. Named constants rather than literals because the ordering is an
    // acceptance-criteria-level behaviour and a test asserts these values by name.

    /// <summary>The whole normalized name is the query. Nothing outranks this.</summary>
    public const int RankExactName = 0;

    /// <summary>The normalized name begins with the query.</summary>
    public const int RankNamePrefix = 1;

    /// <summary>A word inside the name begins with the query — "kumar" finding "ravi kumar".</summary>
    public const int RankNameWordPrefix = 2;

    /// <summary>The query appears somewhere inside the name.</summary>
    public const int RankNameSubstring = 3;

    /// <summary>The stored phone is exactly the digits typed.</summary>
    public const int RankPhoneExact = 4;

    /// <summary>The stored phone ends with the digits typed — the last-4 case (E-59).</summary>
    public const int RankPhoneSuffix = 5;

    /// <summary>The digits appear somewhere in the stored phone.</summary>
    public const int RankPhoneSubstring = 6;

    /// <summary>Matched only by name similarity, on the fallback path (E-30).</summary>
    public const int RankFuzzyName = 7;

    /// <summary>Reached only if a row is returned that no rule explains. Sorts last.</summary>
    public const int RankUnmatched = 99;

    /// <summary>
    /// Whether a patient is offered at all, before any query is considered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Inactive and merged records are hidden by default (plan F-7 point 1).</b> Both are
    /// records the physician has already decided are not the one to attach new work to — a retired
    /// patient (F-8) and a duplicate that points at a survivor (F-6). Offering them in the ordinary
    /// picker is the RSK-12 wrong-patient path with the extra insult that the clinic already knew.
    /// </para>
    /// <para>
    /// <b>One toggle covers both</b>, because the plan describes one ("an 'include inactive'
    /// toggle"), and because the reason to turn it on is the same in both cases: you are looking
    /// for an old record on purpose. When it is on, the rows still carry their status, so a merged
    /// record is visibly a merged record and never looks like an ordinary choice.
    /// </para>
    /// </remarks>
    public static Expression<Func<Patient, bool>> IsSelectable(bool includeInactive) =>
        includeInactive
            ? _ => true
            : p => p.Status == PatientStatus.Active && p.MergedIntoPatientId == null;

    /// <summary>
    /// The <c>WHERE</c> clause for a search: selectable, and matching the query by name or phone.
    /// </summary>
    /// <param name="normalizedQuery">
    /// The typed text through <see cref="PatientNormalizer.NormalizeName(string?)"/>.
    /// </param>
    /// <param name="digits">
    /// The typed text through <see cref="PatientNormalizer.NormalizePhone(string?)"/>, or null when
    /// it contained no digits at all.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Name and phone are one query, not two modes.</b> The physician types into one box and
    /// does not tell the application which kind of thing they typed; BRD L93 asks for search "by
    /// name or phone number" and the honest reading of that is a single field that tries both.
    /// A query with digits in it is matched against both — a name really can contain a digit, and
    /// refusing to try would be a silent miss.
    /// </para>
    /// <para>
    /// <b>Phone matching is <c>Contains</c>, which subsumes the last-4 case (E-59).</b> The plan
    /// calls out the suffix match specifically because that is what a physician remembers, but a
    /// middle fragment is just as likely off a scrap of paper, and there is no cost to accepting
    /// it. <c>Contains</c> on the normalized digits column means <c>"+91 98765-43210"</c>,
    /// <c>"098765 43210"</c> and <c>"9876543210"</c> are all found by typing <c>3210</c>.
    /// </para>
    /// </remarks>
    public static Expression<Func<Patient, bool>> Matches(
        bool includeInactive,
        string normalizedQuery,
        string? digits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);

        var selectable = IsSelectable(includeInactive);

        // Two expressions rather than one with a `digits != null ?` inside it: a closure-captured
        // null check would still be emitted into the SQL as a parameter comparison, and the
        // no-digits query would carry a dead LIKE against the phone column on every keystroke.
        if (digits is null)
        {
            return Combine(selectable, p => p.NormalizedName.Contains(normalizedQuery));
        }

        return Combine(
            selectable,
            p => p.NormalizedName.Contains(normalizedQuery)
                 || (p.NormalizedPhone != null && p.NormalizedPhone.Contains(digits)));
    }

    /// <summary>
    /// The <c>ORDER BY</c> rank for a search — the plan's "exact → prefix → substring → phone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why name outranks phone even for an exact phone match.</b> That is the plan's stated
    /// order, and in practice the buckets barely meet: a query is either letters or digits, and a
    /// digits query almost never substring-matches a name. Following the plan literally here costs
    /// nothing real and keeps the ranking the plan owner confirms the same as the one that ships.
    /// </para>
    /// <para>
    /// <b>The word-prefix bucket is an addition, and it is flagged.</b> The plan lists
    /// exact → prefix → substring → phone. Rank <see cref="RankNameWordPrefix"/> sits between
    /// prefix and substring because in a clinic where many patients share a surname, typing that
    /// surname should list the people whose <em>name begins with it at a word boundary</em> above
    /// people who merely contain the letters. It is a refinement inside the plan's ordering, never
    /// a reordering of it — every bucket the plan names keeps its relative position.
    /// </para>
    /// </remarks>
    public static Expression<Func<Patient, int>> Rank(string normalizedQuery, string? digits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);

        // The space prefix is how "begins at a word boundary" is expressed without a regex EF
        // cannot translate: " kumar" occurs in "ravi kumar" and not in "ravikumar".
        var wordPrefix = " " + normalizedQuery;

        if (digits is null)
        {
            return p =>
                p.NormalizedName == normalizedQuery ? RankExactName
                : p.NormalizedName.StartsWith(normalizedQuery) ? RankNamePrefix
                : p.NormalizedName.Contains(wordPrefix) ? RankNameWordPrefix
                : p.NormalizedName.Contains(normalizedQuery) ? RankNameSubstring
                : RankUnmatched;
        }

        return p =>
            p.NormalizedName == normalizedQuery ? RankExactName
            : p.NormalizedName.StartsWith(normalizedQuery) ? RankNamePrefix
            : p.NormalizedName.Contains(wordPrefix) ? RankNameWordPrefix
            : p.NormalizedName.Contains(normalizedQuery) ? RankNameSubstring
            : p.NormalizedPhone == digits ? RankPhoneExact
            : p.NormalizedPhone != null && p.NormalizedPhone.EndsWith(digits) ? RankPhoneSuffix
            : p.NormalizedPhone != null && p.NormalizedPhone.Contains(digits) ? RankPhoneSubstring
            : RankUnmatched;
    }

    /// <summary>
    /// Clamps a caller-supplied <c>take</c> into <c>1..<see cref="MaxTake"/></c>.
    /// </summary>
    /// <remarks>
    /// Clamped rather than rejected: a client asking for 1,000 rows has made a client mistake, and
    /// answering with 50 is more useful than a 400. Zero and negatives fall back to the default,
    /// because "give me no results" is never what was meant.
    /// </remarks>
    public static int ClampTake(int? take, int fallback) =>
        take is null or <= 0 ? fallback : Math.Min(take.Value, MaxTake);

    /// <summary>
    /// ANDs two predicates into one expression tree EF Core can translate.
    /// </summary>
    /// <remarks>
    /// Hand-built rather than <c>Expression.AndAlso(left.Body, right.Body)</c> on its own: the two
    /// lambdas have different parameter instances, and combining their bodies without rebinding one
    /// of them produces a tree referencing a parameter that is not in scope. EF's translator
    /// reports that as an unrelated-looking failure at query time, so it is worth the six lines.
    /// </remarks>
    private static Expression<Func<Patient, bool>> Combine(
        Expression<Func<Patient, bool>> left,
        Expression<Func<Patient, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(Patient), "p");

        var body = Expression.AndAlso(
            new ParameterRebinder(left.Parameters[0], parameter).Visit(left.Body)!,
            new ParameterRebinder(right.Parameters[0], parameter).Visit(right.Body)!);

        return Expression.Lambda<Func<Patient, bool>>(body, parameter);
    }

    private sealed class ParameterRebinder : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterRebinder(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
