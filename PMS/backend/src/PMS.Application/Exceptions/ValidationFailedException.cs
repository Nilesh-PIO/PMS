namespace PMS.Application.Exceptions;

/// <summary>
/// Thrown when request data fails validation. Mapped to HTTP 400 with a field-keyed
/// <c>errors</c> object (section 7 Error handling), matching the shape ASP.NET Core's own
/// model-state validation produces, so the frontend has exactly one error shape to render.
/// </summary>
public class ValidationFailedException : Exception
{
    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } })
    {
    }

    /// <summary>Field name -> one or more messages for that field.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
