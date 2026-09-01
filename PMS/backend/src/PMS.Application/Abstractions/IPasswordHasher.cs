namespace PMS.Application.Abstractions;

/// <summary>
/// Turns a plaintext password into a storable hash and verifies one against it.
/// Declared here and implemented in PMS.Infrastructure so the application layer never
/// takes a dependency on a specific KDF (section 2, Data access).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a self-describing hash string safe to persist in AppUser.PasswordHash.</summary>
    string Hash(string password);

    /// <summary>
    /// Verifies <paramref name="password"/> against a stored hash. Returns false rather than
    /// throwing for a malformed or empty hash - a corrupted row must fail closed, not 500.
    /// </summary>
    bool Verify(string password, string storedHash);
}
