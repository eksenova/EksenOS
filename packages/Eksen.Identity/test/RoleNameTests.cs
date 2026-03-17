using Eksen.ErrorHandling;
using Eksen.Identity.Roles;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.Tests;

public class RoleNameTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Return_RoleName_When_Valid()
    {
        // Arrange & Act
        var roleName = RoleName.Create("Admin");

        // Assert
        roleName.Value.ShouldBe("Admin");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var roleName = RoleName.Create("  Admin  ");

        // Assert
        roleName.Value.ShouldBe("Admin");
    }

    [Fact]
    public void Create_Should_Throw_When_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<EksenException>(() => RoleName.Create(""));
    }

    [Fact]
    public void Create_Should_Throw_When_Null()
    {
        // Arrange & Act & Assert
        Should.Throw<EksenException>(() => RoleName.Create(null!));
    }

    [Fact]
    public void Create_Should_Throw_When_Whitespace()
    {
        // Arrange & Act & Assert
        Should.Throw<EksenException>(() => RoleName.Create("   "));
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longName = new string('A', RoleName.MaxLength + 1);

        // Act & Assert
        Should.Throw<EksenException>(() => RoleName.Create(longName));
    }

    [Fact]
    public void Create_Should_Succeed_At_MaxLength()
    {
        // Arrange
        var name = new string('A', RoleName.MaxLength);

        // Act
        var roleName = RoleName.Create(name);

        // Assert
        roleName.Value.ShouldBe(name);
    }

    [Fact]
    public void MaxLength_Should_Be_50()
    {
        // Assert
        RoleName.MaxLength.ShouldBe(50);
    }

    [Fact]
    public void Parse_Should_Create_RoleName()
    {
        // Arrange & Act
        var roleName = RoleName.Parse("Editor");

        // Assert
        roleName.Value.ShouldBe("Editor");
    }

    [Fact]
    public void ToParseableString_Should_Return_Value()
    {
        // Arrange
        var roleName = RoleName.Create("Admin");

        // Act
        var result = roleName.ToParseableString();

        // Assert
        result.ShouldBe("Admin");
    }

    [Fact]
    public void Equality_Should_Work_For_Same_Values()
    {
        // Arrange
        var roleName1 = RoleName.Create("Admin");
        var roleName2 = RoleName.Create("Admin");

        // Assert
        roleName1.ShouldBe(roleName2);
    }

    [Fact]
    public void Equality_Should_Fail_For_Different_Values()
    {
        // Arrange
        var roleName1 = RoleName.Create("Admin");
        var roleName2 = RoleName.Create("Editor");

        // Assert
        roleName1.ShouldNotBe(roleName2);
    }
}
