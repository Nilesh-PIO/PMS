using System.Security.Cryptography;
using System.Text;
using PMS.Application.Abstractions;

namespace PMS.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing, using only the BCL so no third-party crypto package
/// enters the dependency graph for a single-user login.
/// </summary>
/// <remarks>
/// <para>
/// Stored format: <c>PBKDF2-SHA256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 subkey&gt;</c>.
/// The iteration count is stored inside the hash rather than assumed at verification time, so
/// raising <see cref="Iterations"/> later keeps every existing credential verifiable instead of
/// locking the physician out - which, with no recovery path until F-21 resolves C-44, would be
/// unrecoverable.
/// </para>
/// <para>
/// Full ASP.NET Core Identity was not used: it brings its own user/role schema, which would
/// collide with the plan's own <c>AppUser</c> entity (section 4). Section 2's decision is the
/// cookie <em>handler</em>, which is used, not the Identity stack.
/// </para>
/// </remarks>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <summary>Algorithm marker, so a future migration to a different KDF can be detected per row.</summary>
    private const string Prefix = "PBKDF2-SHA256";

    /// <summary>OWASP's 2023 floor for PBKDF2-HMAC-SHA256.</summary>
    private const int Iterations = 210_000;

    private const int SaltBytes = 16;
    private const int SubkeyBytes = 32;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, SubkeyBytes);

        return string.Join('$', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(subkey));
    }

    /// <inheritdoc />
    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4
            || !string.Equals(parts[0], Prefix, StringComparison.Ordinal)
            || !int.TryParse(parts[1], out var iterations)
            || iterations <= 0)
        {
            // A row we cannot parse fails closed. Throwing here would turn a corrupted
            // credential row into a 500 on the login page rather than a clean "sign-in failed".
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, Algorithm, expected.Length);

        // Constant-time: a short-circuiting comparison leaks how many leading bytes matched.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
