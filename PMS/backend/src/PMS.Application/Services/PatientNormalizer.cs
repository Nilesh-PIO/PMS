using System.Globalization;
using System.Text;

namespace PMS.Application.Services;

/// <summary>
/// Turns a name and a phone number as typed into the forms used for matching
/// (planning-pms-verification.md, F-5 point 2; brainstorm REC-18, E-59, E-60).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is its own type.</b> F-5 writes these values, F-6 compares them and F-7 searches
/// them. If each derived its own form, the three would drift, and the failure mode of that drift is
/// a duplicate patient nobody can see — the exact split-history problem in E-25. One function per
/// value, called from one place.
/// </para>
/// <para>
/// <b>What normalizing is not allowed to do:</b> change what is stored and displayed. The entered
/// spelling stays untouched on <c>Patient.FullName</c> and <c>Patient.PrimaryPhone</c>. These are
/// additional derived columns, never a replacement.
/// </para>
/// </remarks>
public static class PatientNormalizer
{
    /// <summary>
    /// Trims, collapses internal whitespace, and case-folds (E-60).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three specific decisions, each of which changes who counts as a duplicate:
    /// </para>
    /// <list type="number">
    ///   <item><description><b>All Unicode whitespace collapses</b>, not just the ASCII space. A
    ///   name pasted from a document can carry a non-breaking space, and to a human eye it is
    ///   identical to the typed version — which is precisely the invisible near-duplicate E-60 is
    ///   about.</description></item>
    ///   <item><description><b>Invariant lowercasing</b>, not the current culture. Culture-aware
    ///   casing is not stable across machines (the Turkish dotless i is the classic example), and a
    ///   matching key whose value depends on a server's locale is a key that stops matching after a
    ///   deployment.</description></item>
    ///   <item><description><b>Unicode is preserved, never stripped or transliterated</b> (E-57).
    ///   Folding a Devanagari or Cyrillic name down to ASCII would collapse genuinely different
    ///   people onto one key, which is worse than the duplicate it was trying to prevent. Text is
    ///   normalized to form C so that two encodings of the same accented character agree.</description></item>
    /// </list>
    /// </remarks>
    public static string NormalizeName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return string.Empty;
        }

        // Form C first: "é" written as one code point and as "e" + combining accent are the same
        // name, and comparing them before composing them would call them different people.
        var composed = fullName.Normalize(NormalizationForm.FormC);

        var builder = new StringBuilder(composed.Length);
        var pendingSpace = false;

        foreach (var ch in composed)
        {
            if (char.IsWhiteSpace(ch))
            {
                // Buffered rather than appended, so trailing whitespace never reaches the result
                // and a run of any length becomes exactly one space.
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Reduces a phone number to its digits (E-59), or null when there are none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>"+91 98765-43210"</c>, <c>"098765 43210"</c> and <c>"9876543210"</c> are one number to a
    /// physician and three strings to a database. This collapses the formatting the clinic actually
    /// uses instead of rejecting it — the brainstorm is explicit that the fix is to index a
    /// normalized form, <em>not</em> to police the input.
    /// </para>
    /// <para>
    /// <b>Returns null rather than an empty string</b> when nothing digit-like is present. That
    /// keeps "no phone recorded" distinguishable from "a phone whose digits normalized away", and
    /// it is what stops every phone-less patient from sharing one blank matching key and being
    /// offered to each other as duplicates in F-6.
    /// </para>
    /// <para>
    /// Digits are recognised by Unicode category, so Devanagari or Arabic-Indic numerals are read
    /// as the digits they are, then mapped to their ASCII value so the stored key is comparable.
    /// </para>
    /// </remarks>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var builder = new StringBuilder(phone.Length);

        foreach (var ch in phone)
        {
            if (char.IsDigit(ch))
            {
                // CharUnicodeInfo gives the numeric value of any Unicode decimal digit; '0' + that
                // lands it back in ASCII so two scripts writing the same number agree.
                var value = CharUnicodeInfo.GetDecimalDigitValue(ch);
                builder.Append(value >= 0 ? (char)('0' + value) : ch);
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    /// <summary>
    /// The last four digits of a phone number, for the disambiguating picker (REC-12), or null.
    /// </summary>
    /// <remarks>
    /// Shorter numbers return in full — four digits is a maximum, not a requirement. A three-digit
    /// extension is still more identifying than nothing, and padding or hiding it would only make
    /// two patients look more alike.
    /// </remarks>
    public static string? PhoneTail(string? phone)
    {
        var digits = NormalizePhone(phone);

        if (digits is null)
        {
            return null;
        }

        return digits.Length <= 4 ? digits : digits[^4..];
    }
}
