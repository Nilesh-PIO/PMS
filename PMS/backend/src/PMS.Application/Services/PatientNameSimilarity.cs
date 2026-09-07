namespace PMS.Application.Services;

/// <summary>
/// How alike two normalized patient names are, on a 0..1 scale
/// (planning-pms-verification.md, F-7 point 1 and F-6 point 1; brainstorm E-25, E-30).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why F-7 owns a similarity function at all.</b> E-30 is "returning patient re-registered
/// because search failed to find them (typo in stored name)", and the brainstorm names fuzzy search
/// as <em>the real fix</em>. An exact-substring search cannot find a record stored as
/// <c>"Ravi Kumr"</c> when the physician types <c>"Ravi Kumar"</c>, so the patient is registered a
/// second time and their history splits — which is the whole failure this feature exists to
/// prevent. The fallback is therefore part of F-7's data-integrity mechanism, not a nicety.
/// </para>
/// <para>
/// <b>COORDINATION NOTE for the plan owner and for whoever merges second.</b> Plan F-7 point 1 says
/// the fuzzy fallback "uses the same similarity function as F-6". F-6 (duplicate detection) is
/// being built in parallel on its own branch and is not merged, so this file is F-7's copy of that
/// one function, written with no F-6 dependency so it can stand alone today. <b>When F-6 and F-7
/// meet, there must be exactly one of these types, not two</b> — the plan is explicit that both
/// features compare names the same way, and two implementations that drift apart would mean the
/// duplicate check and the search disagree about who is the same person. Whichever branch merges
/// second should delete its own copy and point at this one rather than keeping both.
/// </para>
/// <para>
/// <b>Levenshtein ratio, not a trigram index.</b> The plan offers "trigram/Levenshtein ratio" as
/// alternatives. Levenshtein is chosen because SQL Server has no built-in trigram similarity, so a
/// trigram approach would mean either a CLR function or a second shadow table — real schema work
/// for a clinic whose whole patient table is a few thousand rows and whose fuzzy path only runs
/// when the exact search already returned nothing.
/// </para>
/// </remarks>
public static class PatientNameSimilarity
{
    /// <summary>
    /// Similarity threshold F-7's search fallback uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ASSUMPTION (plan F-7 point 1, C-22 — no <c>Q-</c> exists for it).</b> The plan pins
    /// <b>0.85</b> as F-6's duplicate-detection threshold and says F-7 reuses "the same similarity
    /// <em>function</em>" — it does not say the same threshold, and the two decisions are not the
    /// same decision:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>F-6 is deciding <b>"warn the physician that this may already exist"</b>
    ///   during registration. A false positive there interrupts someone mid-task, so a strict
    ///   threshold is right.</description></item>
    ///   <item><description>F-7 is deciding <b>"offer this row after finding nothing at all"</b>.
    ///   A false positive costs one extra line on a screen that is otherwise empty; a false
    ///   negative costs a duplicate patient record and a split clinical history (E-30). The costs
    ///   are wildly asymmetric, and only in one direction.</description></item>
    /// </list>
    /// <para>
    /// So the fallback runs at <b>0.7</b>: loose enough to find <c>"Ravi Kumr"</c> from
    /// <c>"Ravi Kumar"</c> (0.90) and <c>"Sunita"</c> from <c>"Suneeta"</c> (0.86), tight enough
    /// that <c>"Ravi"</c> does not surface <c>"Kavita"</c> (0.33). <b>Pinned by
    /// <c>PatientSearchServiceTests</c>, so changing it is a deliberate act with a failing test
    /// rather than a quiet drift.</b> If the plan owner rules that one number must govern both
    /// features, this constant is the only thing that changes.
    /// </para>
    /// </remarks>
    public const double SearchFallbackThreshold = 0.7;

    /// <summary>
    /// Similarity between two already-normalized names, 0 (nothing in common) to 1 (identical).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both inputs are expected to have been through
    /// <see cref="PatientNormalizer.NormalizeName(string?)"/> — case-folded, whitespace-collapsed,
    /// Unicode form C. Comparing a raw name against a normalized one would report two spellings of
    /// one person as different people, which is the bug this whole area exists to avoid.
    /// </para>
    /// <para>
    /// <b>Whole-string and per-word, whichever is kinder.</b> A physician searching a clinic full
    /// of shared surnames types the part they remember, so <c>"kumr"</c> has to be able to find
    /// <c>"ravi kumar"</c> even though whole-string similarity between those two is only 0.4. The
    /// score is therefore the best of: the whole-string ratio, the ratio against each word of the
    /// stored name, and the ratio against each word of the query. Taking the maximum can only ever
    /// <em>widen</em> the fallback, and widening is the safe direction here.
    /// </para>
    /// </remarks>
    public static double Score(string? normalizedQuery, string? normalizedName)
    {
        if (string.IsNullOrEmpty(normalizedQuery) || string.IsNullOrEmpty(normalizedName))
        {
            return 0;
        }

        var best = Ratio(normalizedQuery, normalizedName);

        // Short-circuit before the word loops: nothing below can beat an exact match.
        if (best >= 1.0)
        {
            return 1.0;
        }

        foreach (var nameWord in normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            best = Math.Max(best, Ratio(normalizedQuery, nameWord));
        }

        foreach (var queryWord in normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            best = Math.Max(best, Ratio(queryWord, normalizedName));

            foreach (var nameWord in normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                best = Math.Max(best, Ratio(queryWord, nameWord));
            }
        }

        return best;
    }

    /// <summary>
    /// <c>1 - distance / max(length)</c> for two strings.
    /// </summary>
    private static double Ratio(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return 1.0;
        }

        var longest = Math.Max(a.Length, b.Length);
        return 1.0 - ((double)Distance(a, b) / longest);
    }

    /// <summary>
    /// Levenshtein edit distance, two rows of working memory rather than a full matrix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Compared by <c>char</c>, which is a UTF-16 code unit rather than a user-visible character.
    /// For the scripts a clinic actually registers names in — Latin, Devanagari, Cyrillic, Arabic —
    /// every character is a single code unit after form-C composition, so this is exact. A name
    /// built from astral-plane code points would be measured slightly pessimistically; that is a
    /// marginally narrower fallback for such a name, never a wrong match, and never affects the
    /// exact search path.
    /// </para>
    /// <para>
    /// O(n·m) time with n and m bounded by the 200-character name column, and it only runs on the
    /// fallback path — after an exact search has already come back empty.
    /// </para>
    /// </remarks>
    private static int Distance(string a, string b)
    {
        // Keep the shorter string on the row axis so the working arrays stay small.
        if (a.Length > b.Length)
        {
            (a, b) = (b, a);
        }

        var previous = new int[a.Length + 1];
        var current = new int[a.Length + 1];

        for (var i = 0; i <= a.Length; i++)
        {
            previous[i] = i;
        }

        for (var j = 1; j <= b.Length; j++)
        {
            current[0] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                var substitution = previous[i - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                var insertion = current[i - 1] + 1;
                var deletion = previous[i] + 1;

                current[i] = Math.Min(substitution, Math.Min(insertion, deletion));
            }

            (previous, current) = (current, previous);
        }

        return previous[a.Length];
    }
}
