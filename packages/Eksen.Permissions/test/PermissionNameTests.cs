using Eksen.Core;
using Eksen.ErrorHandling;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class PermissionNameTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Return_PermissionName_When_Valid()
    {
        // Arrange & Act
        var name = PermissionName.Create("Orders.Create");

        // Assert
        name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var name = PermissionName.Create("  Orders.Create  ");

        // Assert
        name.Value.ShouldBe("Orders.Create");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => PermissionName.Create(value!));
        exception.Descriptor.ShouldBe(PermissionErrors.EmptyPermissionName);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', PermissionName.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => PermissionName.Create(longValue));
        exception.Descriptor.ShouldBe(PermissionErrors.PermissionNameOverflow);
    }

    [Fact]
    public void MaxLength_Should_Be_50()
    {
        // Assert
        PermissionName.MaxLength.ShouldBe(50);
    }

    [Fact]
    public void Create_Should_Accept_MaxLength_Value()
    {
        // Arrange
        var value = new string('a', PermissionName.MaxLength);

        // Act
        var name = PermissionName.Create(value);

        // Assert
        name.Value.ShouldBe(value);
    }

    [Fact]
    public void Parse_Should_Return_PermissionName()
    {
        // Arrange & Act
        var name = PermissionName.Parse("Orders.Create");

        // Assert
        name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public void Implicit_Conversion_Should_Work()
    {
        // Arrange & Act
        PermissionName name = "Orders.Create";

        // Assert
        name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public void ToParseableString_Should_Return_Value()
    {
        // Arrange
        var name = PermissionName.Create("Orders.Create");

        // Act
        var result = name.ToParseableString();

        // Assert
        result.ShouldBe("Orders.Create");
    }
}
