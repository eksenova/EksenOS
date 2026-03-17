using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.Tests;

public class UserTypeTests : EksenUnitTestBase
{
    [Fact]
    public void Host_Should_Have_Expected_Value()
    {
        ((int)UserType.Host).ShouldBe(0);
    }

    [Fact]
    public void Tenant_Should_Have_Expected_Value()
    {
        ((int)UserType.Tenant).ShouldBe(1);
    }

    [Fact]
    public void Anonymous_Should_Have_Expected_Value()
    {
        ((int)UserType.Anonymous).ShouldBe(2);
    }

    [Fact]
    public void Enum_Should_Have_Three_Values()
    {
        // Assert
        Enum.GetValues<UserType>().Length.ShouldBe(3);
    }
}
