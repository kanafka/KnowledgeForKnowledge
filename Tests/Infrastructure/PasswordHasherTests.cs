using FluentAssertions;
using Infrastructure.Services;

namespace Tests.InfraServices;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        var hash = _hasher.Hash("mypassword");
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Hash_SamePasswordTwice_ReturnsDifferentHashes()
    {
        // BCrypt uses random salt per call
        var hash1 = _hasher.Hash("password123");
        var hash2 = _hasher.Hash("password123");
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("correct_password");
        _hasher.Verify("correct_password", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("correct_password");
        _hasher.Verify("wrong_password", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_EmptyPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("nonempty");
        _hasher.Verify("", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_LongPassword_Works()
    {
        var longPassword = new string('a', 200);
        var hash = _hasher.Hash(longPassword);
        _hasher.Verify(longPassword, hash).Should().BeTrue();
    }

    [Fact]
    public void Hash_SpecialChars_Works()
    {
        var special = "P@$$w0rd!#%^&*()";
        var hash = _hasher.Hash(special);
        _hasher.Verify(special, hash).Should().BeTrue();
    }

    [Theory]
    [InlineData("Password1")]
    [InlineData("another_secure_pass_2025")]
    [InlineData("Привет123")]
    public void HashAndVerify_RoundTrip_Succeeds(string password)
    {
        var hash = _hasher.Hash(password);
        _hasher.Verify(password, hash).Should().BeTrue();
    }
}
