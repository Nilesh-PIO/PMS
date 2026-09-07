using System.Globalization;

namespace PMS.Application.Services;

/// <summary>
/// How alike two normalized patient names are, on a 0-to-1 scale
/// (planning-pms-verification.md, F-6 point 1 and F-7 point 1; brainstorm REC-2, RSK-2, E-25,
/// E-30).
/// </summary>
/// <remarks>
/// <b>One function, two thresholds — and that is on purpose.</b> Plan F-7 point 1 says the search
/// fallback "uses the same similarity function as F-6", and this is the only place either feature
/// measures name distance: <see cref="Ratio"/> for F-6's duplicate check at
/// <see cref="DefaultThreshold"/>, <see cref="Score"/> for F-7's search fallback at
/// <see cref="SearchFallbackThreshold"/>. Two implementations that drifted apart would mean the
/// duplicate check and the search disagreed about who is the same person. The thresholds stay
/// separate because they answer different questions at different costs; the algorithm underneath
/// them does not.
/// </remarks>
/// <remarks>
/// <para>
/// <b>Why a similarity score and not string equality.</b> Exact matching catches the duplicate
/// nobody creates. The duplicate that actually happens is <c>"Ravi Kumar"</c> against
/// <c>"Ravi Kumaar"</c>, or a name typed once with a middle initial and once without — E-30's
/// "returning patient re-registered because search failed to find them". Those need a measure of
/// distance, not a boolean.
/// </para>
/// <para>
/// <b>The measure is a Levenshtein ratio</b>, per plan F-6 point 1's "trigram/Levenshtein ratio":
/// <c>1 - editDistance / lengthOfTheLongerName</c>. It is deterministic, has no tuning beyond the
/// threshold, and is trivially explainable to the physician whose registration it just interrupted —
/// which matters, because an unexplainable warning is a warning that gets clicked through.
/// </para>
/// <para>
/// <b>Two deliberate refinements over a textbook implementation:</b>
/// </para>
/// <list type="number">
///   <item><description><b>Distance is measured over grapheme clusters, not UTF-16 code units.</b>
///   A Devanagari consonant plus its matra, or an emoji-range character, is one unit to a reader and
///   two or more to <c>string.Length</c>. Measuring in code units would make a Devanagari name score
///   as though it were twice as long as it looks, and quietly hold non-Latin names to a stricter
///   threshold than Latin ones (E-57).</description></item>
///   <item><description><b>Word order is not held against a name.</b> The score is the better of
///   the two names compared as typed and compared with their words sorted, so <c>"Ravi Kumar"</c>
///   and <c>"Kumar Ravi"</c> score 1.0 rather than 0.36. Given-name-first versus family-name-first
///   is not a spelling difference, and C-18/E-13 are explicit that this application does not model
///   a name as first + last, so it cannot reorder them structurally either. This only ever
///   <em>raises</em> a score, so it can add a dismissible warning and can never suppress
///   one.</description></item>
/// </list>
/// <para>
/// <b>ASSUMPTION (plan F-6 point 1, Q-13):</b> the plan names "trigram/Levenshtein ratio" and the
/// 0.85 threshold but not the exact function. The two refinements above are this implementation's
/// choices. Both widen recall rather than narrowing it, which is the safe direction under REC-2's
/// warn-never-block design.
/// </para>
/// </remarks>
public static class NameSimilarity
{
    /// <summary>
    /// The score at or above which two names are treated as possibly the same person
    /// (plan F-6 point 1, Q-13).
    /// </summary>
    /// <remarks>
    /// One constant, referenced everywhere, so the answer to Q-13 is a one-line change rather than a
    /// hunt. It is <em>not</em> a clinical judgement and not derived from any authority — it is the
    /// number the plan states, and the plan states it as the thing Q-13 must confirm.
    /// </remarks>
    public const double DefaultThreshold = 0.85;

    /// <summary>
    /// Scores two <em>already normalized</em> names (see
    /// <see cref="PatientNormalizer.NormalizeName"/>) from 0 (nothing in common) to 1 (identical).
    /// </summary>
    /// <remarks>
    /// Takes normalized input on purpose. Case folding and whitespace collapsing are already
    /// decided in one place, and re-deriving them here would give this function a second opinion
    /// about what a name is.
    /// </remarks>
    public static double Ratio(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            // An empty name is not "very different from" anything — there is simply nothing to
            // compare, and returning a small non-zero score would let a blank drift over a
            // threshold on short names.
            return 0d;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1d;
        }

        var asTyped = RatioOfElements(TextElements(left), TextElements(right));

        var sortedLeft = SortWords(left);
        var sortedRight = SortWords(right);

        if (string.Equals(sortedLeft, left, StringComparison.Ordinal)
            && string.Equals(sortedRight, right, StringComparison.Ordinal))
        {
            // Neither name had its words moved, so the second comparison would be the first one
            // again.
            return asTyped;
        }

        var reordered = RatioOfElements(TextElements(sortedLeft), TextElements(sortedRight));

        return Math.Max(asTyped, reordered);
    }

    /// <summary>True when two normalized names are similar enough to warrant a duplicate warning.</summary>
    public static bool IsSimilar(string? left, string? right, double threshold = DefaultThreshold) =>
        Ratio(left, right) >= threshold;

    // --- F-7's fuzzy search fallback ----------------------------------------
    //
    // MERGE NOTE (F-6 + F-7). F-7 was built in parallel on its own branch and shipped its own copy
    // of this algorithm as `PatientNameSimilarity`, with a coordination comment saying the branch
    // that merged second should collapse the two into one. This is that collapse. There is now one
    // Levenshtein implementation, one grapheme-cluster split and one Ratio; F-7's contribution is
    // the query-shaped scoring below, which sits on top of the same primitive rather than beside a
    // second copy of it. The two *thresholds* deliberately remain two constants - see
    // SearchFallbackThreshold.

    /// <summary>
    /// The score at or above which F-7's search fallback offers a row as a possible match
    /// (plan F-7 point 1, C-22).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ASSUMPTION (plan F-7 point 1, C-22 — brainstorm §12 carries no <c>Q-</c> for it).</b> The
    /// plan pins <b>0.85</b> as F-6's duplicate-detection threshold and says F-7 reuses "the same
    /// similarity <em>function</em>" — it does not say the same threshold, and the two are not the
    /// same decision:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>F-6 decides <b>"interrupt this registration to warn that the person may
    ///   already exist"</b>. A false positive there interrupts someone mid-task, so a strict
    ///   threshold is right.</description></item>
    ///   <item><description>F-7 decides <b>"offer this row after the exact search found nothing at
    ///   all"</b>. A false positive costs one extra line on an otherwise empty screen; a false
    ///   negative costs a duplicate patient record and a split clinical history (E-30). The costs
    ///   are asymmetric, and only in one direction.</description></item>
    /// </list>
    /// <para>
    /// So the fallback runs at <b>0.7</b>: loose enough to find <c>"ravi kumr"</c> from
    /// <c>"ravi kumar"</c> and <c>"suneeta"</c> from <c>"sunita"</c>, tight enough that
    /// <c>"ravi"</c> does not surface <c>"kavita sharma"</c>. Both constants are pinned by tests, so
    /// changing either is a deliberate act with a failing test rather than a quiet drift. If the
    /// plan owner rules that one number must govern both features, these two constants are the only
    /// things that change.
    /// </para>
    /// </remarks>
    public const double SearchFallbackThreshold = 0.7;

    /// <summary>
    /// Scores an already-normalized <em>search query</em> against an already-normalized stored name,
    /// 0 (nothing in common) to 1 (identical).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is not just <see cref="Ratio"/>.</b> <c>Ratio</c> compares two whole names, which
    /// is the right question for F-6: both sides are complete names of complete people. F-7 asks a
    /// different question — one side is a <em>fragment the physician typed</em>. A clinic full of
    /// shared surnames means <c>"kumr"</c> has to be able to reach <c>"ravi kumar"</c>, and the
    /// whole-string ratio between those two is only 0.4.
    /// </para>
    /// <para>
    /// <b>Whole-string and per-word, whichever is kinder.</b> The score is the best of the
    /// whole-string ratio, the query against each word of the stored name, each query word against
    /// the whole name, and each query word against each name word. Taking the maximum can only
    /// <em>widen</em> the fallback, and widening is the safe direction when the alternative is a
    /// duplicate record (E-30). It runs only after the exact search has already returned nothing.
    /// </para>
    /// </remarks>
    public static double Score(string? normalizedQuery, string? normalizedName)
    {
        if (string.IsNullOrEmpty(normalizedQuery) || string.IsNullOrEmpty(normalizedName))
        {
            return 0d;
        }

        var best = Ratio(normalizedQuery, normalizedName);

        // Short-circuit before the word loops: nothing below can beat an exact match.
        if (best >= 1d)
        {
            return 1d;
        }

        var queryWords = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nameWords = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var nameWord in nameWords)
        {
            best = Math.Max(best, Ratio(normalizedQuery, nameWord));
        }

        foreach (var queryWord in queryWords)
        {
            best = Math.Max(best, Ratio(queryWord, normalizedName));

            foreach (var nameWord in nameWords)
            {
                best = Math.Max(best, Ratio(queryWord, nameWord));
            }
        }

        return best;
    }

    /// <summary>The name's words in a stable order, so word order is not mistaken for misspelling.</summary>
    private static string SortWords(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length < 2)
        {
            return value;
        }

        Array.Sort(words, StringComparer.Ordinal);
        return string.Join(' ', words);
    }

    /// <summary>
    /// Splits into grapheme clusters — what a reader would call "one character".
    /// </summary>
    private static string[] TextElements(string value)
    {
        var elements = new List<string>(value.Length);
        var enumerator = StringInfo.GetTextElementEnumerator(value);

        while (enumerator.MoveNext())
        {
            elements.Add((string)enumerator.Current);
        }

        return [.. elements];
    }

    private static double RatioOfElements(string[] left, string[] right)
    {
        var longest = Math.Max(left.Length, right.Length);

        if (longest == 0)
        {
            return 0d;
        }

        var distance = LevenshteinDistance(left, right);

        return 1d - ((double)distance / longest);
    }

    /// <summary>
    /// Edit distance between two sequences of grapheme clusters.
    /// </summary>
    /// <remarks>
    /// Two rows rather than a full matrix: names are bounded at 200 characters by the column, so
    /// this is never large, but a service that runs it over every candidate on every registration
    /// has no reason to allocate a 200x200 matrix each time.
    /// </remarks>
    private static int LevenshteinDistance(string[] left, string[] right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var substitutionCost =
                    string.Equals(left[i - 1], right[j - 1], StringComparison.Ordinal) ? 0 : 1;

                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
