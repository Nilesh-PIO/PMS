using PMS.Application.Dtos.Patients;

namespace PMS.Application.Exceptions;

/// <summary>
/// Thrown when a registration matches a patient already on file and the caller has not confirmed
/// that they mean to create a second record (planning-pms-verification.md, F-6 point 3; brainstorm
/// REC-2, E-25).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a question, not a refusal</b>, and the distinction is load-bearing. REC-2 is explicit
/// that the duplicate check must warn and never block: a blocking rule turns a false positive into a
/// patient who cannot be registered, and the front desk's response to that is to invent a different
/// spelling — which produces the duplicate the rule was trying to prevent, now with a name nobody
/// can search for. So the 409 this maps to is an interruption the caller answers with
/// <c>?confirmDuplicate=true</c>, and the answer is always accepted.
/// </para>
/// <para>
/// <b>Nothing has been written when this is thrown.</b> It is raised before the insert, which is
/// what plan F-6 acceptance criterion 1 requires — "returns 409 with candidates <em>before any row
/// is written</em>". A check that fired after the insert would be a duplicate report, not a
/// duplicate check.
/// </para>
/// <para>
/// Derives from <see cref="DomainRuleException"/> so the existing error contract carries it without
/// a new branch: 409, RFC-7807 body, machine-readable <c>ruleType</c>. The middleware adds the
/// candidate list as an extension, because a warning the caller cannot show the physician is a
/// warning they can only obey or ignore.
/// </para>
/// </remarks>
public sealed class DuplicatePatientException : DomainRuleException
{
    /// <summary>
    /// The machine-readable slug on the 409. The React client branches on this to open the
    /// duplicate dialog rather than rendering a generic conflict message.
    /// </summary>
    public const string DuplicateConfirmationRequired = "duplicate-confirmation-required";

    public DuplicatePatientException(IReadOnlyList<DuplicateCandidateResponse> candidates)
        : base(DuplicateConfirmationRequired, BuildMessage(candidates))
    {
        Candidates = candidates;
    }

    /// <summary>
    /// Everything the check returned — the suspected duplicates <em>and</em> any other patients
    /// reachable on the same phone number.
    /// </summary>
    /// <remarks>
    /// The household rows travel along even though they are not what triggered the refusal. "Three
    /// other people already use this number" is exactly the context that lets the physician tell a
    /// genuine duplicate from a sibling in ten seconds rather than thirty (E-27, E-28).
    /// </remarks>
    public IReadOnlyList<DuplicateCandidateResponse> Candidates { get; }

    /// <summary>
    /// Counts only the suspected duplicates, because those are what the physician is being asked
    /// about. A message that counted the whole household would overstate the problem, and a warning
    /// that overstates is a warning that gets discounted.
    /// </summary>
    private static string BuildMessage(IReadOnlyList<DuplicateCandidateResponse> candidates)
    {
        var likely = candidates.Count(c => c.IsLikelyDuplicate);

        return likely == 1
            ? "A patient already on file looks like the same person. "
              + "Check the match, then confirm to register this patient anyway."
            : $"{likely} patients already on file look like the same person. "
              + "Check the matches, then confirm to register this patient anyway.";
    }
}
