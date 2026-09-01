namespace PMS.Application.Exceptions;

/// <summary>
/// Thrown by a service when an addressed resource does not exist. Mapped to HTTP 404
/// ProblemDetails by ProblemDetailsMiddleware.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityType, string entityId)
        : base($"{entityType} '{entityId}' was not found.")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    public string EntityType { get; }

    public string EntityId { get; }
}
