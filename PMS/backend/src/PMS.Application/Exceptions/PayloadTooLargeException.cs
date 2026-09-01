namespace PMS.Application.Exceptions;

/// <summary>
/// Thrown when an upload exceeds the size a feature accepts. Mapped by
/// ProblemDetailsMiddleware to HTTP 413, which is the status F-3's signature-upload route
/// specifies (planning-pms-verification.md, F-3 point 3).
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="ValidationFailedException"/>. A 400 tells the client "the shape
/// of your request is wrong", and a physician who has just picked a 3 MB photograph of their
/// signature needs to be told the file is too big, not that the form is malformed - the fix is a
/// different file, not a different field.
/// </remarks>
public class PayloadTooLargeException : Exception
{
    public PayloadTooLargeException(string message, long limitBytes)
        : base(message)
    {
        LimitBytes = limitBytes;
    }

    /// <summary>The accepted maximum, echoed to the client so the message can name it.</summary>
    public long LimitBytes { get; }
}
