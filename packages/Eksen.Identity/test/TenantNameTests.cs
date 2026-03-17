using Eksen.ErrorHandling;
using Eksen.Identity.Tenants;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.Tests;

public class TenantNameTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Return_TenantName_When_Valid()
    {
        // Arrange & Act
        var tenantName = TenantName.Create("Acme Corp");

        // Assert
        tenantName.Value.ShouldBe("Acme Corp");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var tenantName = TenantName.Create("  Acme Corp  ");

        // Assert
        tenantName.Value.ShouldBe("Acme Corp");
    }

    [Fact]
    public void Create_Should_Throw_When_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<EksenException>(() => TenantName.Create(""));
    }

    [Fact]
    public void Create_Should_Throw_When_Null()
    {
        // Arrange & Act & Assert
        Should.Throw<EksenException>(() => TenantName.Create(null!));
    }

    [Fact]
    public void Create_Should_Throw_When_Whitespace()
    {
        // Arrange & Act & Assert
        Should.Throw<EksenException>(() => TenantName.Create("   "));
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longName = new string('A', TenantName.MaxLength + 1);

        // Act & Assert
        Should.Throw<EksenException>(() => TenantName.Create(longName));
    }

    [Fact]
    public void Create_Should_Succeed_At_MaxLength()
    {
        // Arrange
        var name = new string('A', TenantName.MaxLength);

        // Act
        var tenantName = TenantName.Create(name);

        // Assert
        tenantName.Value.ShouldBe(name);
    }

    [Fact]
    public void MaxLength_Should_Be_50()
    {
        // Assert
        TenantName.MaxLength.ShouldBe(50);
    }

    [Fact]
    public void Parse_Should_Create_TenantName()
    {
        // Arrange & Act
        var tenantName = TenantName.Parse("Acme Corp");

        // Assert
        tenantName.Value.ShouldBe("Acme Corp");
    }

    [Fact]
    public void ToParseableString_Should_Return_Value()
    {
        // Arrange
        var tenantName = TenantName.Create("Acme Corp");

        // Act
        var result = tenantName.ToParseableString();

        // Assert
        result.ShouldBe("Acme Corp");
    }

    [Fact]
    public void Equality_Should_Work_For_Same_Values()
    {
        // Arrange
        var name1 = TenantName.Create("Acme");
        var name2 = TenantName.Create("Acme");

        // Assert
        name1.ShouldBe(name2);
    }

    [Fact]
    public void Equality_Should_Fail_For_Different_Values()
    {
        // Arrange
        var name1 = TenantName.Create("Acme");
        var name2 = TenantName.Create("Globex");

        // Assert
        name1.ShouldNotBe(name2);
    }
}
