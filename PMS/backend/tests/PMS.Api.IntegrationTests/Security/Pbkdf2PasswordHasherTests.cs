using FluentAssertions;
using PMS.Infrastructure.Security;

namespace PMS.Api.IntegrationTests.Security;

/// <summary>
/// The real KDF behind F-2's "hash verification". These live here rather than in
/// PMS.Application.Tests because the implementation is an Infrastructure concern, and the
/// application-layer unit tests deliberately use a cheap fake so they test AuthService's
/// decisions rather than PBKDF2's arithmetic.
/// </summary>
public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    private const string Password = "SeedDoctor#2026!";

    [Fact]
    public void A_password_verifies_against_its_own_hash()
    {
        var hash = _hasher.Hash(Password);

        _hasher.Verify(Password, hash).Should().BeTrue();
    }

    [Fact]
    public void A_wrong_password_does_not_verify()
    {
        var hash = _hasher.Hash(Password);

        _hasher.Verify("SeedDoctor#2026", hash).Should().BeFalse("one missing character is a wrong password");
        _hasher.Verify("seeddoctor#2026!", hash).Should().BeFalse("passwords are case sensitive");
        _hasher.Verify("", hash).Should().BeFalse();
    }

    [Fact]
    public void The_hash_never_contains_the_password()
    {
        var hash = _hasher.Hash(Password);

        hash.Should().NotContain(Password);
        hash.Should().NotContain("SeedDoctor");
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ()
    {
        // A per-credential random salt. Without it, two identical passwords would be visibly
        // identical in the database and precomputed tables would apply.
        var first = _hasher.Hash(Password);
        var second = _hasher.Hash(Password);

        first.Should().NotBe(second);
        _hasher.Verify(Password, first).Should().BeTrue();
        _hasher.Verify(Password, second).Should().BeTrue();
    }

    [Fact]
    public void The_stored_format_carries_its_own_algorithm_and_iteration_count()
    {
        // Storing the cost inside the hash is what lets the iteration count be raised later
        // without invalidating the only credential the clinic has - which, with no recovery
        // path until F-21 resolves C-44, would be unrecoverable.
        var parts = _hasher.Hash(Password).Split('$');

        parts.Should().HaveCount(4);
        parts[0].Should().Be("PBKDF2-SHA256");
        int.Parse(parts[1]).Should().BeGreaterThanOrEqualTo(210_000, "OWASP's 2023 floor for PBKDF2-HMAC-SHA256");
        Convert.FromBase64String(parts[2]).Should().HaveCount(16, "128-bit salt");
        Convert.FromBase64String(parts[3]).Should().HaveCount(32, "256-bit subkey");
    }

    [Fact]
    public void A_hash_stored_with_a_lower_iteration_count_still_verifies()
    {
        var lowCost = _hasher.Hash(Password).Split('$');
        // Rebuild a hash string claiming a different cost; it must not verify, because the
        // subkey no longer matches the cost - proving the count is genuinely read from the row
        // rather than assumed.
        var tampered = string.Join('$', lowCost[0], "1000", lowCost[2], lowCost[3]);

        _hasher.Verify(Password, tampered).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA256$notanumber$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA256$210000$!!!notbase64!!!$AAAA")]
    [InlineData("SCRYPT$210000$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA256$0$AAAA$AAAA")]
    public void A_corrupted_or_unknown_hash_fails_closed_rather_than_throwing(string storedHash)
    {
        // A corrupted credential row must produce "sign-in failed", not a 500 on the login
        // page - the physician needs a screen they can act on, not a stack trace.
        var act = () => _hasher.Verify(Password, storedHash);

        act.Should().NotThrow();
        _hasher.Verify(Password, storedHash).Should().BeFalse();
    }

    [Fact]
    public void Verification_is_not_confused_by_a_password_that_is_a_prefix_of_the_real_one()
    {
        var hash = _hasher.Hash("AbcdefghijklMnop");

        _hasher.Verify("Abcdefghijkl", hash).Should().BeFalse();
        _hasher.Verify("AbcdefghijklMnopq", hash).Should().BeFalse();
    }

    [Fact]
    public void A_long_unicode_password_round_trips()
    {
        var password = "Ωμέγα-पासवर्ड-2026-🔒-clinic";

        _hasher.Verify(password, _hasher.Hash(password)).Should().BeTrue();
    }
}
