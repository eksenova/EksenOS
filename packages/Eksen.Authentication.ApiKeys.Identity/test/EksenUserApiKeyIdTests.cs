using Eksen.TestBase;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Identity.Tests;

public class EksenUserApiKeyIdTests : EksenUnitTestBase
{
    [Fact]
    public void NewId_Should_Create_Non_Empty_Id()
    {
        // Arrange & Act
        var id = EksenUserApiKeyId.NewId();

        // Assert
        id.ShouldNotBe(EksenUserApiKeyId.Empty);
    }

    [Fact]
    public void Empty_Should_Return_Empty_Id()
    {
        // Arrange & Act
        var id = EksenUserApiKeyId.Empty;

        // Assert
        id.Value.ShouldBe(System.Ulid.Empty);
    }

    [Fact]
    public void Two_Ids_With_Same_Value_Should_Be_Equal()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id1 = new EksenUserApiKeyId(ulid);
        var id2 = new EksenUserApiKeyId(ulid);

        // Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void Two_Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange & Act
        var id1 = EksenUserApiKeyId.NewId();
        var id2 = EksenUserApiKeyId.NewId();

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void ToString_Should_Return_Ulid_String()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id = new EksenUserApiKeyId(ulid);

        // Act
        var result = id.ToString();

        // Assert
        result.ShouldContain(ulid.ToString());
    }
}
