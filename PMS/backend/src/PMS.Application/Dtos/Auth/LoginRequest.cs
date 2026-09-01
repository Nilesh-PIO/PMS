namespace PMS.Application.Dtos.Auth;

/// <summary>
/// Request DTO for POST /api/auth/login and POST /api/auth/reauth
/// (planning-pms-verification.md, F-2 point 3).
/// A DTO, not an entity - no EF type ever crosses the wire (section 2, API shape).
/// </summary>
public sealed record LoginRequest(string? UserName, string? Password);
