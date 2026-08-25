namespace PMS.Application.Exceptions;

/// <summary>
/// Thrown by a service when a domain rule forbids the requested action. Mapped by
/// ProblemDetailsMiddleware to HTTP 409 with a machine-readable <see cref="RuleType"/>
/// (planning-pms-verification.md, section 7 Error handling).
/// </summary>
/// <remarks>
/// F-1 introduces the contract; later features throw it with their own rule types, e.g.
/// "setup-incomplete" (F-3), "visit-already-finalized" (F-10), "illegal-status-transition" (F-9).
/// </remarks>
public class DomainRuleException : Exception
{
    public DomainRuleException(string ruleType, string message)
        : base(message)
    {
        RuleType = ruleType;
    }

    /// <summary>
    /// Stable, machine-readable slug the frontend can branch on. Never a localised sentence.
    /// </summary>
    public string RuleType { get; }
}
