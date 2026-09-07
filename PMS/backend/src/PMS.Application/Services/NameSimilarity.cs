using System.Globalization;

namespace PMS.Application.Services;

/// <summary>
/// How alike two normalized patient names are, on a 0-to-1 scale
/// (planning-pms-verification.md, F-6 point 1; brainstorm REC-2, RSK-2, E-25, E-30).
/// </summary>
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
