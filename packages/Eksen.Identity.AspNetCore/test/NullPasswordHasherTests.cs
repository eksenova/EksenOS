using Eksen.Identity.AspNetCore.Security;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.AspNetCore.Tests;

public class NullPasswordHasherTests : EksenUnitTestBase
{
    private readonly NullPasswordHasher<object> _sut = new();

    [Fact]
    public void HashPassword_Should_Throw_NotSupportedException()
    {
        // Act & Assert
        Should.Throw<NotSupportedException>(
            () => _sut.HashPassword(new object(), "password"));
    }

    [Fact]
    public void VerifyHashedPassword_Should_Throw_NotSupportedException()
    {
        // Act & Assert
        Should.Throw<NotSupportedException>(
            () => _sut.VerifyHashedPassword(new object(), "hash", "password"));
    }
}
