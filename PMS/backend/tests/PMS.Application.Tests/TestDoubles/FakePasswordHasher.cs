using System.Text;
using PMS.Application.Abstractions;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// A deterministic, reversible stand-in for the real hasher, so AuthService's own decisions can
/// be unit-tested without paying 210,000 PBKDF2 iterations per assertion.
/// </summary>
/// <remarks>
/// This proves nothing about the real KDF, which is deliberately tested separately against
/// <c>Pbkdf2PasswordHasher</c> in PMS.Api.IntegrationTests/Security, and end to end by the
/// auth endpoint tests. Using a fake here and only here keeps the two concerns from hiding
/// each other.
/// </remarks>
public sealed class FakePasswordHasher : IPasswordHasher
{
    private const string Prefix = "fake:";

    /// <summary>Counts verifications, so a test can prove the unknown-user path still hashes (timing equalisation).</summary>
    public int VerifyCount { get; private set; }

    /// <summary>
    /// Deterministic and cheap, but deliberately not the plaintext: a fake that stored the
    /// password verbatim would make "the stored value never contains the password" pass or
    /// fail for reasons that have nothing to do with the code under test.
    /// </summary>
    public string Hash(string password) =>
        Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(new string(password.Reverse().ToArray())));

    public bool Verify(string password, string storedHash)
    {
        VerifyCount++;
        return storedHash == Hash(password);
    }
}
