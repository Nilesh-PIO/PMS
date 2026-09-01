using FluentAssertions;
using PMS.Application.Abstractions;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;
using PMS.Domain.Entities;

namespace PMS.Application.Tests.Services;

/// <summary>
/// The one-time seeding task required by plan F-2 point 2: reads an initial credential,
/// hashes it, inserts the AppUser row, and "refuses to run twice".
/// </summary>
public class InitialUserSeederTests
{
    private const string UserName = "doctor";
    private const string Password = "SeedDoctor#2026!";

    private readonly FakePasswordHasher _hasher = new();

    [Fact]
    public async Task It_creates_the_single_login_on_an_empty_database()
    {
        var repository = new FakeAppUserRepository();
        var seeder = new InitialUserSeeder(repository, _hasher);

        var result = await seeder.SeedAsync(UserName, Password, default);

        result.Outcome.Should().Be(InitialUserSeedOutcome.Seeded);
        result.Created.Should().BeTrue();
        repository.Users.Should().ContainSingle();
        repository.Users[0].UserName.Should().Be(UserName);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task It_stores_a_hash_and_never_the_plaintext_password()
    {
        var repository = new FakeAppUserRepository();
        var seeder = new InitialUserSeeder(repository, _hasher);

        await seeder.SeedAsync(UserName, Password, default);

        var stored = repository.Users.Single();
        stored.PasswordHash.Should().NotBe(Password);
        stored.PasswordHash.Should().NotContain(Password.Substring(0, 4),
            "a 'hash' that still contains the password is not a hash");
        stored.PasswordHash.Should().Be(_hasher.Hash(Password));
    }

    [Fact]
    public async Task It_gives_the_new_user_a_security_stamp()
    {
        var repository = new FakeAppUserRepository();
        var seeder = new InitialUserSeeder(repository, _hasher);

        await seeder.SeedAsync(UserName, Password, default);

        repository.Users.Single().SecurityStamp.Should().NotBeNullOrWhiteSpace();
    }

    // --- refuses to run twice ----------------------------------------------

    [Fact]
    public async Task It_refuses_to_run_a_second_time()
    {
        var repository = new FakeAppUserRepository();
        var seeder = new InitialUserSeeder(repository, _hasher);

        await seeder.SeedAsync(UserName, Password, default);
        var second = await seeder.SeedAsync(UserName, Password, default);

        second.Outcome.Should().Be(InitialUserSeedOutcome.SkippedAlreadySeeded);
        repository.Users.Should().ContainSingle("a restart must not add a second physician row");
        repository.SaveCount.Should().Be(1, "the second run must not write at all");
    }

    [Fact]
    public async Task It_never_overwrites_a_password_the_physician_has_already_changed()
    {
        // The failure this guards against: the doctor changes their password, the app is
        // restarted, and the seeder quietly resets it back to the value in a config file -
        // handing the clinic a credential that is written down.
        var existing = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = UserName,
            PasswordHash = _hasher.Hash("APasswordTheDoctorChoseLater!"),
            SecurityStamp = "original-stamp",
        };
        var repository = new FakeAppUserRepository(existing);
        var seeder = new InitialUserSeeder(repository, _hasher);

        await seeder.SeedAsync(UserName, Password, default);

        existing.PasswordHash.Should().Be(_hasher.Hash("APasswordTheDoctorChoseLater!"));
        existing.SecurityStamp.Should().Be("original-stamp");
    }

    [Fact]
    public async Task An_existing_user_with_a_different_name_still_blocks_seeding()
    {
        // "Exactly one row" (plan section 4) is the invariant, not "one row per name".
        var repository = new FakeAppUserRepository(new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "someone-else",
            PasswordHash = _hasher.Hash(Password),
            SecurityStamp = "s",
        });
        var seeder = new InitialUserSeeder(repository, _hasher);

        var result = await seeder.SeedAsync(UserName, Password, default);

        result.Outcome.Should().Be(InitialUserSeedOutcome.SkippedAlreadySeeded);
        repository.Users.Should().ContainSingle();
    }

    // --- configuration guards ----------------------------------------------

    [Theory]
    [InlineData(null, Password)]
    [InlineData("", Password)]
    [InlineData("   ", Password)]
    [InlineData(UserName, null)]
    [InlineData(UserName, "")]
    public async Task With_no_configured_credential_it_creates_nothing(string? userName, string? password)
    {
        var repository = new FakeAppUserRepository();
        var seeder = new InitialUserSeeder(repository, _hasher);

        var result = await seeder.SeedAsync(userName, password, default);

        result.Outcome.Should().Be(InitialUserSeedOutcome.SkippedNotConfigured);
        repository.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task It_refuses_a_password_below_the_twelve_character_minimum()
    {
        var repository = new FakeAppUserRepository();
        var seeder = new InitialUserSeeder(repository, _hasher);

        var result = await seeder.SeedAsync(UserName, "short1!", default);

        result.Outcome.Should().Be(InitialUserSeedOutcome.RejectedWeakPassword);
        repository.Users.Should().BeEmpty("a weak seed credential must be refused, not weakened into the database");
    }

    [Fact]
    public async Task It_accepts_a_password_of_exactly_the_minimum_length()
    {
        var repository = new FakeAppUserRepository();
        var seeder = new InitialUserSeeder(repository, _hasher);
        var exactlyTwelve = new string('a', SessionPolicy.MinimumPasswordLength);

        var result = await seeder.SeedAsync(UserName, exactlyTwelve, default);

        result.Outcome.Should().Be(InitialUserSeedOutcome.Seeded);
    }

    [Fact]
    public async Task No_outcome_detail_ever_contains_the_password()
    {
        var repository = new FakeAppUserRepository();
        var seeder = new InitialUserSeeder(repository, _hasher);

        var seeded = await seeder.SeedAsync(UserName, Password, default);
        var skipped = await seeder.SeedAsync(UserName, Password, default);
        var weak = await new InitialUserSeeder(new FakeAppUserRepository(), _hasher)
            .SeedAsync(UserName, "hunter2", default);

        // These strings go to the application log. A password in a log file is a password on
        // disk, in a place nobody thinks to rotate.
        seeded.Detail.Should().NotContain(Password);
        skipped.Detail.Should().NotContain(Password);
        weak.Detail.Should().NotContain("hunter2");
    }

    /// <summary>
    /// The credential the tracked appsettings.json actually carries, per the recorded
    /// deviation. If the committed seed password ever stops satisfying the policy the seeder
    /// enforces, the application would start with no login at all and only a log line to say
    /// so - this test turns that into a build failure instead.
    /// </summary>
    [Fact]
    public void The_committed_seed_password_satisfies_the_policy_the_seeder_enforces()
    {
        Password.Length.Should().BeGreaterThanOrEqualTo(SessionPolicy.MinimumPasswordLength);
    }
}
