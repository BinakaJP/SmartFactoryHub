using FluentAssertions;
using Identity.API.Security;

namespace Identity.API.Tests.Security;

public class PasswordHasherTests
{
    // ── Hash ────────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_ReturnsStringInExpectedFormat()
    {
        // Arrange & Act
        var hash = PasswordHasher.Hash("SomePassword1!");

        // Assert — format: base64(salt):base64(hash)
        var parts = hash.Split(':');
        parts.Should().HaveCount(2, "the format must be salt:hash");
        parts[0].Should().NotBeNullOrEmpty("salt must be present");
        parts[1].Should().NotBeNullOrEmpty("hash must be present");
        // Both parts must be valid Base64
        FluentActions.Invoking(() => Convert.FromBase64String(parts[0])).Should().NotThrow();
        FluentActions.Invoking(() => Convert.FromBase64String(parts[1])).Should().NotThrow();
    }

    [Fact]
    public void Hash_SamePasswordProducesDifferentHashes_DueToRandomSalt()
    {
        var password = "SamePassword99!";

        var hash1 = PasswordHasher.Hash(password);
        var hash2 = PasswordHasher.Hash(password);

        hash1.Should().NotBe(hash2, "each call generates a fresh random salt");
    }

    [Fact]
    public void Hash_EmptyPassword_DoesNotThrow()
    {
        // Edge case: empty string is a valid (albeit weak) password input
        var act = () => PasswordHasher.Hash(string.Empty);
        act.Should().NotThrow();
    }

    [Fact]
    public void Hash_LongPassword_DoesNotThrow()
    {
        var longPassword = new string('A', 1024);
        var act = () => PasswordHasher.Hash(longPassword);
        act.Should().NotThrow();
    }

    // ── Verify ──────────────────────────────────────────────────────────────

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        const string password = "CorrectHorseBattery1!";
        var hash = PasswordHasher.Hash(password);

        var result = PasswordHasher.Verify(password, hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("RightPassword1!");

        var result = PasswordHasher.Verify("WrongPassword1!", hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_EmptyHashedPassword_ReturnsFalse()
    {
        var result = PasswordHasher.Verify("SomePassword1!", string.Empty);

        result.Should().BeFalse("an empty string has no colon separator");
    }

    [Fact]
    public void Verify_MalformedHash_NoColon_ReturnsFalse()
    {
        var result = PasswordHasher.Verify("password", "notavalidhashstring");

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_InvalidBase64InHash_ReturnsFalse()
    {
        // parts[0] is not valid base64
        var result = PasswordHasher.Verify("password", "!!!notbase64!!!:abc");

        result.Should().BeFalse("invalid base64 should be caught and return false");
    }

    [Fact]
    public void Verify_IsCaseSensitiveForPassword()
    {
        var hash = PasswordHasher.Hash("Password1!");

        PasswordHasher.Verify("password1!", hash).Should().BeFalse("passwords are case-sensitive");
        PasswordHasher.Verify("PASSWORD1!", hash).Should().BeFalse("passwords are case-sensitive");
        PasswordHasher.Verify("Password1!", hash).Should().BeTrue("exact match must succeed");
    }
}
