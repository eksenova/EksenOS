using Eksen.Identity.AspNetCore.Security;
using Eksen.Identity.AspNetCore.Tests.Fakes;
using Eksen.TestBase;
using Microsoft.AspNetCore.Identity;
using Shouldly;

namespace Eksen.Identity.AspNetCore.Tests;

public class EksenUserPasswordHasherTests : EksenUnitTestBase
{
    private readonly EksenUserPasswordHasher<FakeUser, FakeTenant> _sut = new();

    [Fact]
    public void HashPassword_Should_Return_BCrypt_Hash()
    {
        // Arrange
        var user = new FakeUser();
        var password = "StrongP@ssword123";

        // Act
        var hash = _sut.HashPassword(user, password);

        // Assert
        hash.ShouldNotBeNullOrWhiteSpace();
        hash.ShouldStartWith("$2");
    }

    [Fact]
    public void HashPassword_Should_Return_Different_Hashes_For_Same_Password()
    {
        // Arrange
        var user = new FakeUser();
        var password = "StrongP@ssword123";

        // Act
        var hash1 = _sut.HashPassword(user, password);
        var hash2 = _sut.HashPassword(user, password);

        // Assert (different salts → different hashes)
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void VerifyHashedPassword_Should_Return_Success_When_Valid()
    {
        // Arrange
        var user = new FakeUser();
        var password = "StrongP@ssword123";
        var hash = _sut.HashPassword(user, password);

        // Act
        var result = _sut.VerifyHashedPassword(user, hash, password);

        // Assert
        result.ShouldBe(PasswordVerificationResult.Success);
    }

    [Fact]
    public void VerifyHashedPassword_Should_Return_Failed_When_Invalid()
    {
        // Arrange
        var user = new FakeUser();
        var hash = _sut.HashPassword(user, "CorrectPassword");

        // Act
        var result = _sut.VerifyHashedPassword(user, hash, "WrongPassword");

        // Assert
        result.ShouldBe(PasswordVerificationResult.Failed);
    }
}
