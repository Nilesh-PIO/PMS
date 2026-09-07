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
    /// This removes <em>punctuation and spacing</em>. <c>"+91 98765-43210"</c>,
    /// <c>"+919876543210"</c> and <c>"91 98765 43210"</c> all land on <c>"919876543210"</c>, which
    /// is the point: the brainstorm is explicit that the fix for format variation is to index a
    /// normalized form, <em>not</em> to police the input.
    /// </para>
    /// <para>
    /// <b>What it deliberately does not do — and this paragraph is a correction.</b> An earlier
    /// version of this comment claimed <c>"+91 98765-43210"</c>, <c>"098765 43210"</c> and
    /// <c>"9876543210"</c> were collapsed together. They are not, and never were: digits-only
    /// normalization turns them into <c>"919876543210"</c>, <c>"09876543210"</c> and
    /// <c>"9876543210"</c> — three keys for one number, because a country code and a trunk zero are
    /// digits too. Making those three <em>match</em> is a matching decision rather than a
    /// normalization one, and it is F-6's: see <see cref="PhoneMatchKey"/>. This method's output
    /// stays the faithful digit sequence, so the stored column never discards information a future
    /// revision of the matching rule might want back.
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

    // --- F-6: the phone matching key (Q-13) ---------------------------------

    /// <summary>
    /// The most significant digits a phone number can be matched on — at most
    /// <see cref="PhoneMatchDigits"/> of them, with one leading trunk zero removed first.
    /// </summary>
    public const int PhoneMatchDigits = 10;

    /// <summary>
    /// Below this many digits a number carries too little identity to match on, and produces no
    /// key at all.
    /// </summary>
    /// <remarks>
    /// A three- or four-digit extension is a real thing to record and a terrible thing to match on:
    /// every patient reachable on "extension 204" would be offered as a duplicate of every other.
    /// Six is the floor at which a number is plausibly a whole number rather than a fragment.
    /// </remarks>
    public const int MinimumPhoneMatchDigits = 6;

    /// <summary>
    /// The key two phone numbers are compared on for duplicate detection (F-6, Q-13), or null when
    /// the number is absent or too short to identify anyone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ASSUMPTION (plan F-6 point 1, Q-13) — this is the open decision F-6 had to settle, and it
    /// needs the plan owner's confirmation.</b> The plan's identity rule is "same
    /// <c>NormalizedPhone</c>", but <see cref="NormalizePhone"/> is digits-only, so
    /// <c>"+91 98765 43210"</c> stores as <c>919876543210</c> while <c>"098765 43210"</c> stores as
    /// <c>09876543210</c>. Under literal string equality those are two different people. They are
    /// obviously one person, and a duplicate check that misses them misses the single most likely
    /// way one patient gets registered twice — the same number typed once with a country code and
    /// once with a trunk zero.
    /// </para>
    /// <para>
    /// So "same phone" is defined here as <b>equal on the last
    /// <see cref="PhoneMatchDigits"/> significant digits</b>, derived in two steps:
    /// </para>
    /// <list type="number">
    ///   <item><description><b>Drop one leading zero.</b> A domestic trunk prefix is dialling
    ///   syntax, not part of the number. Only one is dropped — <c>"00"</c>-style international
    ///   prefixes are handled by step 2 rather than by stripping zeros until something looks
    ///   right.</description></item>
    ///   <item><description><b>Keep the last ten digits.</b> That discards a country code of any
    ///   length without this code having to carry a table of them, and it is what finally makes
    ///   <c>919876543210</c>, <c>09876543210</c> and <c>9876543210</c> one key.</description></item>
    /// </list>
    /// <para>
    /// <b>The cost of this choice, stated rather than hidden:</b> two numbers that agree in their
    /// last ten digits but belong to different countries would match. In a single-physician local
    /// clinic that is close to impossible, and the consequence if it ever happens is bounded — the
    /// phone key is <em>never</em> sufficient on its own. F-6's rule requires a name similarity of
    /// at least <see cref="NameSimilarity.DefaultThreshold"/> as well, and the result only ever
    /// raises a dismissible warning (REC-2: warn, never block). The opposite error — failing to
    /// match, and splitting one patient's history across two records — is rated Critical (E-25) and
    /// is not recoverable by clicking anything.
    /// </para>
    /// <para>
    /// Kept separate from <see cref="NormalizePhone"/> on purpose: the stored column keeps every
    /// digit the physician typed, and only this derived key is lossy. If the owner's answer to Q-13
    /// differs — scope cross-country matching out, or use a different digit count — this method and
    /// the matching backfill in <c>AddPatientDuplicateIndexes</c> are the only two places that
    /// change.
    /// </para>
    /// </remarks>
    public static string? PhoneMatchKey(string? phone)
    {
        var digits = NormalizePhone(phone);

        if (digits is null)
        {
            return null;
        }

        // Step 1: one trunk zero, and only one.
        if (digits.Length > 1 && digits[0] == '0')
        {
            digits = digits[1..];
        }

        // Step 2: the country code, whatever length it is, falls off the front.
        if (digits.Length > PhoneMatchDigits)
        {
            digits = digits[^PhoneMatchDigits..];
        }

        // Too short to be an identity signal. Null rather than a short key, so these numbers are
        // absent from matching rather than all matching each other.
        return digits.Length >= MinimumPhoneMatchDigits ? digits : null;
    }
}
