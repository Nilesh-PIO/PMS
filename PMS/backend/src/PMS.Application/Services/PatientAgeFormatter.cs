using System.Globalization;
using PMS.Domain.Entities;

namespace PMS.Application.Services;

/// <summary>
/// Renders a patient's age as a string that never hides how it was known
/// (planning-pms-verification.md, F-5 point 1; brainstorm C-19, E-9, E-11, E-21).
/// </summary>
/// <remarks>
/// <para>
/// <b>One formatter, server-side, for every consumer.</b> The age appears on the profile, in F-7's
/// picker and on F-14's printed prescription. If each rendered its own, they would eventually
/// disagree, and the printed sheet — the one the patient takes home — would be the copy nobody
/// could correct. So the string is computed once, here, and shipped in the DTO.
/// </para>
/// <para>
/// <b>The output always says which kind of age it is.</b> An exact age from a date of birth reads
/// <c>"41"</c>. An estimate reads <c>"~40 (recorded 2026)"</c> — the tilde and the year together are
/// what stop an approximation from being read years later as a fact (E-21). An unknown age reads
/// <c>"Age not recorded"</c> and never <c>"0"</c>, because a sentinel number in a clinical field is
/// the E-18 mistake in a different column.
/// </para>
/// </remarks>
public static class PatientAgeFormatter
{
    /// <summary>What is shown when neither a date of birth nor an approximate age was recorded.</summary>
    public const string NotRecorded = "Age not recorded";

    /// <summary>
    /// Formats the age of <paramref name="patient"/> as of <paramref name="today"/>.
    /// </summary>
    public static string Format(Patient patient, DateOnly today)
    {
        if (patient.DateOfBirth is { } dob)
        {
            return FormatExact(dob, today);
        }

        if (patient.ApproxAgeYears is { } approx && patient.AgeRecordedOn is { } recordedOn)
        {
            return FormatApproximate(approx, recordedOn);
        }

        return NotRecorded;
    }

    /// <summary>
    /// An exact age from a known date of birth, in the unit that carries information at that age
    /// (E-11).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Years is the wrong unit for a newborn: "0" is what a paediatric dosing decision must not be
    /// told. So under one month the age is in days, under two years in months, and in years
    /// thereafter. The thresholds are the plan's, not invented here.
    /// </para>
    /// <para>
    /// A date of birth of <em>today</em> is a real value — a newborn seen on the day of birth — and
    /// renders as <c>"0 days"</c>. That is genuinely different from
    /// <see cref="NotRecorded"/>: one means born today, the other means nobody asked.
    /// </para>
    /// </remarks>
    private static string FormatExact(DateOnly dateOfBirth, DateOnly today)
    {
        if (dateOfBirth > today)
        {
            // Rejected at the service layer, so reaching here means the row predates that rule or
            // was written directly in SSMS. Say so rather than render a negative age.
            return "Date of birth is in the future";
        }

        var days = today.DayNumber - dateOfBirth.DayNumber;

        if (days < 31)
        {
            return days == 1 ? "1 day" : $"{days} days";
        }

        var months = ((today.Year - dateOfBirth.Year) * 12) + today.Month - dateOfBirth.Month;
        if (today.Day < dateOfBirth.Day)
        {
            // The current month is not complete yet.
            months--;
        }

        if (months < 24)
        {
            return months == 1 ? "1 month" : $"{months} months";
        }

        var years = today.Year - dateOfBirth.Year;
        if (today.Month < dateOfBirth.Month
            || (today.Month == dateOfBirth.Month && today.Day < dateOfBirth.Day))
        {
            // Birthday has not happened yet this year.
            years--;
        }

        return years.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// An approximate age, shown with the year it was taken (E-21).
    /// </summary>
    /// <remarks>
    /// <b>The recorded year is never dropped, and the number is never aged forward silently.</b>
    /// Displaying "~46" in 2032 for an age recorded as 40 in 2026 would present an inference as a
    /// record. What was written down is "about 40, in 2026", so that is what is shown, and the
    /// reader does the arithmetic knowing it is arithmetic.
    /// </remarks>
    private static string FormatApproximate(int approxAgeYears, DateOnly recordedOn) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"~{approxAgeYears} (recorded {recordedOn.Year})");
}
